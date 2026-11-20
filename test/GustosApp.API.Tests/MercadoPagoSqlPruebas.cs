using GustosApp.Application.Common.Exceptions;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model.@enum;
using GustosApp.Infraestructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static GustosApp.API.Tests.MercadoPagoServicePruebas;

namespace GustosApp.API.Tests;

public sealed class SqlMercadoPagoFactAttribute : FactAttribute
{
    public SqlMercadoPagoFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("GUSTOS_EF_SQL_PAGOS") != "1")
            Skip = "Requiere SQL Server local y una base desechable para pagos simulados.";
    }
}

[Collection("SQL Server local")]
public sealed class MercadoPagoSqlPruebas
{
    [SqlMercadoPagoFact]
    public async Task DobleInicioYWebhooksConcurrentes_PersistenUnaCompraYUnPago()
    {
        await ConBaseAisladaAsync(async opciones =>
        {
            await using var inicio = new GustosDbContext(opciones);
            var usuario = await SembrarUsuarioAsync(inicio);
            var entro = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var liberar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var posts = 0;
            using var crear = new Handler(async (request, ct) =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Interlocked.Increment(ref posts);
                await using var lectura = new GustosDbContext(opciones);
                var compraId = await lectura.ComprasPremium.Select(c => c.Id).SingleAsync(ct);
                entro.SetResult();
                await liberar.Task.WaitAsync(ct);
                return Orden(compraId);
            });
            await using var contextoA = new GustosDbContext(opciones);
            await using var contextoB = new GustosDbContext(opciones);
            var primera = CrearServicio(contextoA, crear).IniciarCompraAsync(usuario.FirebaseUid);
            try
            {
                await entro.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await Assert.ThrowsAsync<PagoTemporalmenteNoDisponibleException>(() => CrearServicio(contextoB, crear).IniciarCompraAsync(usuario.FirebaseUid));
            }
            finally { liberar.TrySetResult(); }
            var compra = await primera;
            Assert.Equal(1, posts);

            var consultando = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var terminar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var confirmar = new Handler(async (request, ct) =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                consultando.TrySetResult();
                await terminar.Task.WaitAsync(ct);
                return Orden(compra.CompraId, "processed", "accredited");
            });
            await using var webhookA = new GustosDbContext(opciones);
            await using var webhookB = new GustosDbContext(opciones);
            var procesar = CrearServicio(webhookA, confirmar).ProcesarNotificacionOrdenAsync("ORDTSTPRUEBA1");
            try
            {
                await consultando.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(ResultadoWebhookPago.FalloTransitorio,
                    await CrearServicio(webhookB, confirmar).ProcesarNotificacionOrdenAsync("ORDTSTPRUEBA1"));
                await using var retorno = new GustosDbContext(opciones);
                var estadoGuardado = await CrearServicio(retorno, Handler.SinPeticiones())
                    .ObtenerEstadoCompraAsync(usuario.FirebaseUid, compra.CompraId, true);
                Assert.Equal("EsperandoPago", estadoGuardado.Estado);
                Assert.False(estadoGuardado.AccesoPlus);
            }
            finally { terminar.TrySetResult(); }
            Assert.Equal(ResultadoWebhookPago.Procesado, await procesar);
            Assert.Equal(ResultadoWebhookPago.Procesado, await CrearServicio(webhookB, confirmar).ProcesarNotificacionOrdenAsync("ORDTSTPRUEBA1"));
            Assert.Equal(1, await inicio.ComprasPremium.CountAsync());
            Assert.Equal(1, await inicio.PagosPremiumProveedor.CountAsync());
            Assert.Equal(PlanUsuario.Plus, await inicio.Usuarios.Select(u => u.Plan).SingleAsync());
        });
    }

    [SqlMercadoPagoFact]
    public async Task ProveedorAceptaYFallaElGuardado_ReanudaLaMismaOperacion()
    {
        await ConBaseAisladaAsync(async opciones =>
        {
            await using var inicial = new GustosDbContext(opciones);
            var usuario = await SembrarUsuarioAsync(inicial);
            var claves = new List<string>();
            using var proveedor = new Handler(async (request, ct) =>
            {
                claves.Add(request.Headers.GetValues("X-Idempotency-Key").Single());
                await using var lectura = new GustosDbContext(opciones);
                return Orden(await lectura.ComprasPremium.Select(c => c.Id).SingleAsync(ct));
            });

            var conFallo = new DbContextOptionsBuilder<GustosDbContext>(opciones).AddInterceptors(new FallarAlGuardarOrden()).Options;
            await using (var intento = new GustosDbContext(conFallo))
                await Assert.ThrowsAsync<PagoTemporalmenteNoDisponibleException>(() => CrearServicio(intento, proveedor).IniciarCompraAsync(usuario.FirebaseUid));
            Assert.Null(await inicial.ComprasPremium.Select(c => c.OrdenProveedorId).SingleAsync());
            await using var reintento = new GustosDbContext(opciones);
            var compra = await CrearServicio(reintento, proveedor).IniciarCompraAsync(usuario.FirebaseUid);
            Assert.True(compra.Reutilizada);
            Assert.NotNull(compra.UrlCheckout);
            Assert.Equal(2, claves.Count);
            Assert.Equal(claves[0], claves[1]);
            Assert.Equal(1, await inicial.ComprasPremium.CountAsync());
            Assert.Equal(PlanUsuario.Free, await inicial.Usuarios.Select(u => u.Plan).SingleAsync());
        });
    }

    private static async Task ConBaseAisladaAsync(Func<DbContextOptions<GustosDbContext>, Task> ejecutar)
    {
        var nombre = $"GustosPagosPruebas_{Guid.NewGuid():N}";
        var opciones = new DbContextOptionsBuilder<GustosDbContext>()
            .UseSqlServer($@"Server=lpc:localhost\SQLEXPRESS;Database={nombre};Integrated Security=True;TrustServerCertificate=True;Connect Timeout=30;Pooling=False").Options;
        await using var db = new GustosDbContext(opciones);
        if (await db.Database.CanConnectAsync()) throw new InvalidOperationException("La base de prueba ya existe.");
        Assert.True(await db.Database.EnsureCreatedAsync());
        try { await ejecutar(opciones); }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class FallarAlGuardarOrden : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is GustosDbContext db && db.ComprasPremium.Local.Any(c => c.OrdenProveedorId != null))
                throw new DbUpdateException("Fallo de guardado simulado.");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
