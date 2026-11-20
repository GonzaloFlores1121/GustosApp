using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.NotificacionUseCases;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;

namespace GustosApp.Application.Tests;

public sealed class RecomendacionesSinDisponibilidadPruebas
{
    [Fact]
    public async Task NoEnviaCorreoSiLaBusquedaYaNoTieneRestaurantesDisponibles()
    {
        var activos = new Mock<IUsuariosActivosService>();
        activos.Setup(s => s.GetUsuariosActivos()).Returns(["uid-prueba"]);
        var cache = new Mock<ICacheService>();
        cache.Setup(s => s.GetAsync<UserLocation>("usuario:uid-prueba:location"))
            .ReturnsAsync(new UserLocation(1, 2, 3000, DateTime.UtcNow));
        var usuarios = new Mock<IUsuarioRepository>();
        usuarios.Setup(s => s.GetByFirebaseUidAsync("uid-prueba", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Usuario("uid-prueba", "prueba@example.test", "Nombre", "Apellido", "prueba"));
        var buscar = new Mock<IBuscarRestaurantesRecomendadosOrquestador>();
        buscar.Setup(s => s.HandleAsync("uid-prueba", null, null, 1, 2, 3000, 1, 4, It.IsAny<CancellationToken>(), false)).ReturnsAsync([]);
        var correo = new Mock<IEmailService>();
        var caso = new EnviarRecomendacionesUsuariosActivosUseCase(activos.Object, cache.Object, buscar.Object, correo.Object, usuarios.Object);
        Assert.False(await caso.HandleAsync("admin", default));
        correo.VerifyNoOtherCalls();
    }
}
