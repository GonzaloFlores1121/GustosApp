using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using GustosApp.Infraestructure;
using GustosApp.Infraestructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GustosApp.API.Tests;

public sealed class AprobacionReclamoPersistenciaPruebas
{
    [Fact]
    public async Task AprobarReclamo_ConservaFichaRelacionesYAsignaIdentidadDelPropietario()
    {
        var opciones = new DbContextOptionsBuilder<GustosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var contexto = new GustosDbContext(opciones);

        var usuario = new Usuario(
            "firebase-propietario",
            "propietario@example.test",
            "Ana",
            "Pérez",
            "ana-propietaria")
        {
            Id = Guid.NewGuid(),
            Rol = RolUsuario.Usuario
        };
        var gusto = new Gusto { Id = Guid.NewGuid(), Nombre = "Pizza" };
        var restriccion = new Restriccion { Id = Guid.NewGuid(), Nombre = "Sin gluten" };
        var restauranteId = Guid.NewGuid();
        var restaurante = new Restaurante
        {
            Id = restauranteId,
            PlaceId = "google-place-reclamado",
            Nombre = "Ficha importada",
            Direccion = "Calle 123",
            Categoria = "Pizzería",
            Rating = 4.8,
            MenuProcesado = true,
            GustosQueSirve = new List<Gusto> { gusto },
            RestriccionesQueRespeta = new List<Restriccion> { restriccion },
            Menu = new RestauranteMenu
            {
                RestauranteId = restauranteId,
                Json = "{\"categorias\":[]}",
                Version = 3
            },
            Imagenes = new List<RestauranteImagen>
            {
                new()
                {
                    RestauranteId = restauranteId,
                    Tipo = TipoImagenRestaurante.Interior,
                    Url = "https://imagenes.test/interior.jpg"
                }
            }
        };
        var solicitud = new SolicitudRestaurante
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            Usuario = usuario,
            RestauranteExistenteId = restauranteId,
            Nombre = restaurante.Nombre,
            Direccion = restaurante.Direccion,
            WebsiteUrl = string.Empty,
            Estado = EstadoSolicitudRestaurante.Pendiente
        };

        contexto.AddRange(usuario, restaurante, solicitud);
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();

        var solicitudes = new SolicitudRestauranteRepositoryEF(contexto);
        var restaurantes = new RestauranteRepositoryEF(contexto);
        var firebase = new Mock<IFirebaseAuthService>();
        var email = new Mock<IEmailService>();
        var plantillas = new Mock<IEmailTemplateService>();
        firebase.Setup(servicio => servicio.SetUserRoleAsync(
                usuario.FirebaseUid,
                RolUsuario.DuenoRestaurante.ToString()))
            .Returns(Task.CompletedTask);
        plantillas.Setup(servicio => servicio.Render(
                "SolicitudAprobada.html",
                It.IsAny<Dictionary<string, string>>()))
            .Returns("aprobada");
        email.Setup(servicio => servicio.EnviarEmailAsync(
                usuario.Email,
                It.IsAny<string>(),
                "aprobada",
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var casoDeUso = new AprobarSolicitudRestauranteUseCase(
            solicitudes,
            restaurantes,
            Mock.Of<IUsuarioRepository>(),
            Mock.Of<IOcrService>(),
            Mock.Of<IMenuParser>(),
            Mock.Of<IRestriccionRepository>(),
            Mock.Of<IGustoRepository>(),
            Mock.Of<IRestauranteMenuRepository>(),
            firebase.Object,
            email.Object,
            plantillas.Object,
            Mock.Of<IHttpDownloader>());

        var resultado = await casoDeUso.HandleAsync(solicitud.Id, CancellationToken.None);

        Assert.Equal(restauranteId, resultado.Id);
        contexto.ChangeTracker.Clear();
        var persistido = await contexto.Restaurantes
            .Include(item => item.GustosQueSirve)
            .Include(item => item.RestriccionesQueRespeta)
            .Include(item => item.Imagenes)
            .Include(item => item.Menu)
            .SingleAsync();
        var solicitudPersistida = await contexto.SolicitudesRestaurantes.SingleAsync();
        var usuarioPersistido = await contexto.Usuarios.SingleAsync();

        Assert.Single(await contexto.Restaurantes.ToListAsync());
        Assert.Equal(usuario.Id, persistido.DuenoId);
        Assert.Equal(usuario.FirebaseUid, persistido.PropietarioUid);
        Assert.Equal("google-place-reclamado", persistido.PlaceId);
        Assert.Equal(4.8, persistido.Rating);
        Assert.True(persistido.MenuProcesado);
        Assert.Equal(3, persistido.Menu?.Version);
        Assert.Equal(gusto.Id, Assert.Single(persistido.GustosQueSirve).Id);
        Assert.Equal(restriccion.Id, Assert.Single(persistido.RestriccionesQueRespeta).Id);
        Assert.Equal("https://imagenes.test/interior.jpg", Assert.Single(persistido.Imagenes).Url);
        Assert.Equal(restauranteId, solicitudPersistida.RestauranteAprobadoId);
        Assert.Equal(EstadoSolicitudRestaurante.Aprobada, solicitudPersistida.Estado);
        Assert.Equal(RolUsuario.DuenoRestaurante, usuarioPersistido.Rol);
        Assert.Equal(restauranteId, (await restaurantes.GetByFirebaseUidAsync(usuario.FirebaseUid))?.Id);
    }
}
