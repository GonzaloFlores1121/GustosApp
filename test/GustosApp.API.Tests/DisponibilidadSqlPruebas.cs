using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using GustosApp.Infraestructure;
using GustosApp.Infraestructure.Migrations;
using GustosApp.Infraestructure.Repositories;
using GustosApp.Infraestructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GustosApp.API.Tests;

public sealed class SqlDisponibilidadFactAttribute : FactAttribute
{
    public SqlDisponibilidadFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("GUSTOS_EF_SQL_DISPONIBILIDAD") != "1")
            Skip = "Requiere SQL Server local y una base aislada de disponibilidad.";
    }
}

[Collection("SQL Server local")]
public sealed class DisponibilidadSqlPruebas
{
    [SqlDisponibilidadFact]
    public async Task MigracionAditiva_ConservaFilasExistentesYLasDejaActivasYNoArchivadas()
    {
        await ConBaseAisladaAsync(async opciones =>
        {
            await using var db = new GustosDbContext(opciones);
            var admin = UsuarioNuevo();
            var restaurante = FichaNueva();
            var solicitud = new SolicitudRestaurante { Id = Guid.NewGuid(), Usuario = admin, Nombre = "Rechazo conservado",
                Direccion = "Prueba", WebsiteUrl = "", Estado = EstadoSolicitudRestaurante.Rechazada, MotivoRechazo = "Conservar motivo" };
            db.AddRange(admin, restaurante, solicitud);
            db.Add(new UsuarioRestauranteFavorito { UsuarioId = admin.Id, RestauranteId = restaurante.Id });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            // Reconstruir el esquema previo quitando únicamente las adiciones de esta migración.
            // Después ejecutar las operaciones Up reales sobre filas que ya existían.
            var migracion = new DesactivacionReversibleYArchivoSolicitudes { ActiveProvider = db.Database.ProviderName! };
            var generador = db.GetService<IMigrationsSqlGenerator>();
            foreach (var comando in generador.Generate(migracion.DownOperations, db.Model))
                await db.Database.ExecuteSqlRawAsync(comando.CommandText);
            foreach (var comando in generador.Generate(migracion.UpOperations, db.Model))
                await db.Database.ExecuteSqlRawAsync(comando.CommandText);
            var conservado = await db.Restaurantes.SingleAsync(r => r.Id == restaurante.Id);
            Assert.False(conservado.Desactivado);
            Assert.Equal(0, conservado.VersionDisponibilidad);
            var rechazo = await db.SolicitudesRestaurantes.SingleAsync(s => s.Id == solicitud.Id);
            Assert.False(rechazo.Archivada);
            Assert.Equal("Conservar motivo", rechazo.MotivoRechazo);
            Assert.Equal(EstadoSolicitudRestaurante.Rechazada, rechazo.Estado);
            Assert.True(await db.UsuarioRestauranteFavoritos.AnyAsync(f => f.RestauranteId == restaurante.Id));
            Assert.Empty(await db.RegistrosAdministracionRestaurantes.ToListAsync());
        });
    }

    [SqlDisponibilidadFact]
    public async Task BajaConcurrenteEsperaElVotoQueYaSeEstaConfirmandoYBloqueaLosPosteriores()
    {
        await ConBaseAisladaAsync(async opciones =>
        {
            var admin = UsuarioNuevo();
            var restaurante = FichaNueva();
            var grupo = new Grupo("Grupo SQL disponibilidad", admin.Id);
            var votacion = new VotacionGrupo(grupo.Id) { Grupo = grupo };
            votacion.Participantes.Add(new(votacion.Id, admin.Id));
            votacion.RestaurantesCandidatos.Add(new(votacion.Id, restaurante.Id));
            await using (var inicio = new GustosDbContext(opciones))
            {
                inicio.AddRange(admin, restaurante, votacion);
                await inicio.SaveChangesAsync();
            }
            var pausa = new PausaVoto();
            var opcionesConPausa = new DbContextOptionsBuilder<GustosDbContext>(opciones).AddInterceptors(pausa).Options;
            await using var dbVoto = new GustosDbContext(opcionesConPausa);
            await using var dbBaja = new GustosDbContext(opciones);
            var voto = new VotoRestaurante(votacion.Id, admin.Id, restaurante.Id);
            var guardando = new VotacionRepository(dbVoto).RegistrarVotoAsync(voto);
            await pausa.Entrada.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var baja = new AdministracionRestaurantes(dbBaja, new EfApplicationTransaction(dbBaja), TimeProvider.System)
                .CambiarDisponibilidadAsync(restaurante.Id, new(true, 0, "Baja concurrente"), admin.FirebaseUid, default);
            try
            {
                Assert.NotSame(baja, await Task.WhenAny(baja, Task.Delay(200)));
            }
            finally { pausa.Continuar.TrySetResult(); }
            await guardando.WaitAsync(TimeSpan.FromSeconds(15));
            await baja.WaitAsync(TimeSpan.FromSeconds(15));
            await using var lectura = new GustosDbContext(opciones);
            Assert.True((await lectura.Restaurantes.FindAsync(restaurante.Id))!.Desactivado);
            Assert.Single(await lectura.Votos.Where(v => v.VotacionId == votacion.Id).ToListAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => new VotacionRepository(lectura)
                .RegistrarVotoAsync(new(votacion.Id, Guid.NewGuid(), restaurante.Id)));
        });
    }

    [SqlDisponibilidadFact]
    public async Task ReintentosConcurrentes_NoDuplicanRegistroYUnaVersionViejaNoRevierteLaBaja()
    {
        await ConBaseAisladaAsync(async opciones =>
        {
            var admin = UsuarioNuevo();
            var restaurante = FichaNueva();
            await using (var db = new GustosDbContext(opciones)) { db.AddRange(admin, restaurante); await db.SaveChangesAsync(); }
            async Task IntentarAsync()
            {
                await using var db = new GustosDbContext(opciones);
                try { await new AdministracionRestaurantes(db, new EfApplicationTransaction(db), TimeProvider.System)
                    .CambiarDisponibilidadAsync(restaurante.Id, new(true, 0, "Motivo concurrente"), admin.FirebaseUid, default); }
                catch (InvalidOperationException) { /* Conflicto reintentable, sin una segunda escritura. */ }
            }
            await Task.WhenAll(IntentarAsync(), IntentarAsync());
            await using var final = new GustosDbContext(opciones);
            Assert.True((await final.Restaurantes.FindAsync(restaurante.Id))!.Desactivado);
            Assert.Single(await final.RegistrosAdministracionRestaurantes.ToListAsync());
            var servicio = new AdministracionRestaurantes(final, new EfApplicationTransaction(final), TimeProvider.System);
            await servicio.CambiarDisponibilidadAsync(restaurante.Id, new(true, 0, "Reintento"), admin.FirebaseUid, default);
            await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.CambiarDisponibilidadAsync(restaurante.Id, new(false, 0, "Versión vieja"), admin.FirebaseUid, default));
            var solicitud = new SolicitudRestaurante { Id = Guid.NewGuid(), UsuarioId = admin.Id, Nombre = "Rechazo concurrente", Direccion = "Calle", WebsiteUrl = "", Estado = EstadoSolicitudRestaurante.Rechazada, MotivoRechazo = "Conservar" };
            final.Add(solicitud);
            await final.SaveChangesAsync();
            async Task ArchivarAsync()
            {
                await using var db = new GustosDbContext(opciones);
                try { await new AdministracionRestaurantes(db, new EfApplicationTransaction(db), TimeProvider.System)
                    .CambiarArchivoAsync(solicitud.Id, new(true, 0), admin.FirebaseUid, default); }
                catch (InvalidOperationException) { /* El reintento consulta el estado vigente. */ }
            }
            await Task.WhenAll(ArchivarAsync(), ArchivarAsync());
            Assert.Single(await final.RegistrosAdministracionRestaurantes.Where(r => r.SolicitudId == solicitud.Id).ToListAsync());
            await final.Entry(solicitud).ReloadAsync();
            Assert.True(solicitud.Archivada);
            Assert.Equal(EstadoSolicitudRestaurante.Rechazada, solicitud.Estado);
            Assert.Equal("Conservar", solicitud.MotivoRechazo);
        });
    }

    private sealed class PausaVoto : SaveChangesInterceptor
    {
        public TaskCompletionSource Entrada { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continuar { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData datos,
            InterceptionResult<int> resultado, CancellationToken ct = default)
        {
            Entrada.TrySetResult();
            await Continuar.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
            return resultado;
        }
    }

    private static Usuario UsuarioNuevo() => new(Guid.NewGuid().ToString("N"), "sql@example.test", "Admin", "SQL", Guid.NewGuid().ToString("N")) { Rol = RolUsuario.Admin };
    private static Restaurante FichaNueva() => new() { Id = Guid.NewGuid(), Nombre = "Ficha SQL", NombreNormalizado = "ficha sql", Direccion = "Calle", PlaceId = Guid.NewGuid().ToString("N") };

    private static async Task ConBaseAisladaAsync(Func<DbContextOptions<GustosDbContext>, Task> prueba)
    {
        var nombre = $"GustosDisponibilidadPruebas_{Guid.NewGuid():N}";
        var opciones = new DbContextOptionsBuilder<GustosDbContext>()
            .UseSqlServer($@"Server=lpc:localhost\SQLEXPRESS;Database={nombre};Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10;Pooling=False").Options;
        await using var db = new GustosDbContext(opciones);
        if (await db.Database.CanConnectAsync()) throw new InvalidOperationException("La base aislada ya existe.");
        try { Assert.True(await db.Database.EnsureCreatedAsync()); await prueba(opciones); }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
