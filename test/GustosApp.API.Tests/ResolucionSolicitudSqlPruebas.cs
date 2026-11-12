using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using GustosApp.Infraestructure;
using GustosApp.Infraestructure.Repositories;
using GustosApp.Infraestructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GustosApp.API.Tests;

public sealed class SqlResolucionFactAttribute : FactAttribute
{
    public SqlResolucionFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("GUSTOS_EF_SQL_RESOLUCION") != "1")
            Skip = "Requiere SQL Server local y permiso para una base desechable de pruebas.";
    }
}

[Collection("SQL Server local")]
public sealed class ResolucionSolicitudSqlPruebas
{
    [SqlResolucionFact]
    public async Task DosAprobacionesYDosRechazosConcurrentes_UnCorreoPorResolucionYUnSoloOcr()
    {
        await ConBaseAisladaAsync(async opciones =>
        {
            await VerificarParAsync(opciones, aprobar: true);
            await VerificarParAsync(opciones, aprobar: false);
        });
    }

    [SqlResolucionFact]
    public async Task CancelacionDespuesDeAceptacionSmtp_ConservaIntencionYNoReenvia()
    {
        await ConBaseAisladaAsync(async opciones =>
        {
            var usuario = NuevoUsuario();
            var solicitud = NuevaSolicitud(usuario.Id);
            await using (var inicio = new GustosDbContext(opciones))
            {
                inicio.AddRange(usuario, solicitud);
                await inicio.SaveChangesAsync();
            }
            using var cancelacion = new CancellationTokenSource();
            var email = new Mock<IEmailService>(MockBehavior.Strict);
            email.Setup(x => x.EnviarEmailAsync(usuario.Email, It.IsAny<string>(), "prueba", It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    cancelacion.Cancel();
                    return Task.CompletedTask;
                });
            var plantillas = new Mock<IEmailTemplateService>();
            plantillas.Setup(x => x.Render(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>())).Returns("prueba");

            async Task RechazarAsync(CancellationToken ct)
            {
                await using var db = new GustosDbContext(opciones);
                var solicitudes = new SolicitudRestauranteRepositoryEF(db);
                var procesar = new ProcesarResolucionSolicitudUseCase(solicitudes, new RestauranteRepositoryEF(db),
                    new RestauranteMenuRepositoryEF(db), new BloqueoResolucionSolicitudSql(db),
                    Mock.Of<IHttpDownloader>(), Mock.Of<IOcrService>(), Mock.Of<IMenuParser>(),
                    Mock.Of<IFirebaseAuthService>(), email.Object, plantillas.Object);
                await new RechazarSolicitudRestauranteUseCase(solicitudes, procesar)
                    .HandleAsync(solicitud.Id, "Motivo de prueba", ct);
            }

            await RechazarAsync(cancelacion.Token);
            await using var lectura = new GustosDbContext(opciones);
            var interrumpida = await lectura.SolicitudesRestaurantes.AsNoTracking().SingleAsync(s => s.Id == solicitud.Id);
            Assert.Equal(EstadoSolicitudRestaurante.Rechazada, interrumpida.Estado);
            Assert.Equal(EstadoCorreoResolucion.Enviando, interrumpida.CorreoResolucion);

            await RechazarAsync(default);
            var final = await lectura.SolicitudesRestaurantes.AsNoTracking().SingleAsync(s => s.Id == solicitud.Id);
            Assert.Equal(EstadoCorreoResolucion.Incierto, final.CorreoResolucion);
            Assert.False(final.CorreoRechazoEnviado);
            email.Verify(x => x.EnviarEmailAsync(usuario.Email, It.IsAny<string>(), "prueba", It.IsAny<CancellationToken>()), Times.Once);
        });
    }

    [SqlResolucionFact]
    public async Task Insercion_SoloElIndicePendienteSeTraduceAConflicto()
    {
        await ConBaseAisladaAsync(async opciones =>
        {
            await using var db = new GustosDbContext(opciones);
            var usuario = NuevoUsuario();
            db.Add(usuario);
            await db.SaveChangesAsync();
            var repositorio = new SolicitudRestauranteRepositoryEF(db);
            await repositorio.AddAsync(NuevaSolicitud(usuario.Id), default);
            await Assert.ThrowsAsync<SolicitudRestauranteConcurrenteException>(
                () => repositorio.AddAsync(NuevaSolicitud(usuario.Id), default));
            Assert.Single(await db.SolicitudesRestaurantes.AsNoTracking().ToListAsync());
            await Assert.ThrowsAsync<DbUpdateException>(() => repositorio.AddAsync(NuevaSolicitud(Guid.NewGuid()), default));
        });
    }

    private static async Task VerificarParAsync(DbContextOptions<GustosDbContext> opciones, bool aprobar)
    {
        var usuario = NuevoUsuario();
        var solicitud = NuevaSolicitud(usuario.Id);
        if (aprobar)
            solicitud.Imagenes.Add(new SolicitudRestauranteImagen { Tipo = TipoImagenSolicitud.Menu, Url = "https://example.test/menu.png" });
        await using (var inicio = new GustosDbContext(opciones))
        {
            inicio.AddRange(usuario, solicitud);
            await inicio.SaveChangesAsync();
        }

        var entroAlEnvio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminarEnvio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var email = new Mock<IEmailService>(MockBehavior.Strict);
        email.Setup(x => x.EnviarEmailAsync(usuario.Email, It.IsAny<string>(), "prueba", It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                entroAlEnvio.SetResult();
                await terminarEnvio.Task.WaitAsync(TimeSpan.FromSeconds(20));
            });
        var plantillas = new Mock<IEmailTemplateService>();
        plantillas.Setup(x => x.Render(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>())).Returns("prueba");
        var downloader = new Mock<IHttpDownloader>();
        downloader.Setup(x => x.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([1]);
        var ocr = new Mock<IOcrService>();
        ocr.Setup(x => x.ReconocerTextoAsync(It.IsAny<IEnumerable<Stream>>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("Pizza");
        var parser = new Mock<IMenuParser>();
        parser.Setup(x => x.ParsearAsync("Pizza", "ARS", It.IsAny<CancellationToken>())).ReturnsAsync("{\"categorias\":[]}");
        var firebase = new Mock<IFirebaseAuthService>();
        var gustos = new Mock<IGustoRepository>();
        gustos.Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>(), default)).ReturnsAsync([]);
        var restricciones = new Mock<IRestriccionRepository>();
        restricciones.Setup(r => r.GetRestriccionesByIdsAsync(It.IsAny<List<Guid>>(), default)).ReturnsAsync([]);

        async Task ResolverAsync()
        {
            await using var db = new GustosDbContext(opciones);
            var solicitudes = new SolicitudRestauranteRepositoryEF(db);
            var restaurantes = new RestauranteRepositoryEF(db);
            var procesador = new ProcesarResolucionSolicitudUseCase(solicitudes, restaurantes,
                new RestauranteMenuRepositoryEF(db), new BloqueoResolucionSolicitudSql(db),
                downloader.Object, ocr.Object, parser.Object, firebase.Object, email.Object, plantillas.Object);
            if (aprobar)
                await new AprobarSolicitudRestauranteUseCase(solicitudes, restaurantes, restricciones.Object, gustos.Object, procesador)
                    .HandleAsync(solicitud.Id, default);
            else
                await new RechazarSolicitudRestauranteUseCase(solicitudes, procesador)
                    .HandleAsync(solicitud.Id, "Motivo de prueba", default);
        }

        var primera = ResolverAsync();
        try
        {
            await entroAlEnvio.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await using var lectura = new GustosDbContext(opciones);
            var durante = await lectura.SolicitudesRestaurantes.AsNoTracking().SingleAsync(s => s.Id == solicitud.Id);
            Assert.Equal(EstadoCorreoResolucion.Enviando, durante.CorreoResolucion);
            Assert.Equal(aprobar ? EstadoSolicitudRestaurante.Aprobada : EstadoSolicitudRestaurante.Rechazada, durante.Estado);
            await ResolverAsync().WaitAsync(TimeSpan.FromSeconds(8));
            Assert.False(primera.IsCompleted);
        }
        finally
        {
            terminarEnvio.TrySetResult();
            await primera;
        }
        await ResolverAsync();

        await using var fin = new GustosDbContext(opciones);
        var final = await fin.SolicitudesRestaurantes.AsNoTracking().SingleAsync(s => s.Id == solicitud.Id);
        Assert.Equal(EstadoCorreoResolucion.Aceptado, final.CorreoResolucion);
        Assert.Equal(aprobar, final.CorreoAprobacionEnviado);
        Assert.Equal(!aprobar, final.CorreoRechazoEnviado);
        Assert.Equal(aprobar ? 1 : 0, await fin.Restaurantes.CountAsync(r => r.DuenoId == usuario.Id));
        Assert.Equal(aprobar ? EstadoMenuResolucion.Listo : EstadoMenuResolucion.NoProgramado, final.MenuResolucion);
        email.Verify(x => x.EnviarEmailAsync(usuario.Email, It.IsAny<string>(), "prueba", It.IsAny<CancellationToken>()), Times.Once);
        ocr.Verify(x => x.ReconocerTextoAsync(It.IsAny<IEnumerable<Stream>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), aprobar ? Times.Once() : Times.Never());
    }

    private static Usuario NuevoUsuario()
    {
        var id = Guid.NewGuid().ToString("N");
        return new Usuario(id, $"{id}@example.test", "Prueba", "Aislada", id[..16], null) { Rol = RolUsuario.Usuario };
    }

    private static SolicitudRestaurante NuevaSolicitud(Guid idUsuario) => new()
    {
        Id = Guid.NewGuid(), UsuarioId = idUsuario, Nombre = "Local de prueba", Direccion = "Calle 123", WebsiteUrl = ""
    };

    private static async Task ConBaseAisladaAsync(Func<DbContextOptions<GustosDbContext>, Task> prueba)
    {
        var nombre = $"GustosResolucionPruebas_{Guid.NewGuid():N}";
        var opciones = new DbContextOptionsBuilder<GustosDbContext>()
            .UseSqlServer($@"Server=lpc:localhost\SQLEXPRESS;Database={nombre};Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10;Pooling=False")
            .Options;
        await using var db = new GustosDbContext(opciones);
        if (await db.Database.CanConnectAsync())
            throw new InvalidOperationException("La base de pruebas ya existe.");
        try
        {
            Assert.True(await db.Database.EnsureCreatedAsync());
            await prueba(opciones);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
