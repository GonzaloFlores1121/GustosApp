using FluentAssertions;
using GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;

namespace GustosApp.Application.Tests;

public class ObtenerMiSolicitudRestauranteUseCaseTests
{
    [Fact]
    public async Task HandleAsync_DevuelveLaUltimaSolicitudDelUsuario()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = "uid-prueba" };
        var solicitud = new SolicitudRestaurante { Id = Guid.NewGuid(), UsuarioId = usuario.Id };
        var usuarios = new Mock<IUsuarioRepository>();
        var solicitudes = new Mock<ISolicitudRestauranteRepository>();
        usuarios.Setup(r => r.GetByFirebaseUidAsync(usuario.FirebaseUid, default)).ReturnsAsync(usuario);
        solicitudes.Setup(r => r.BuscarUltimaPorUsuarioAsync(usuario.Id, default)).ReturnsAsync(solicitud);
        var casoDeUso = new ObtenerMiSolicitudRestauranteUseCase(solicitudes.Object, usuarios.Object);

        var resultado = await casoDeUso.HandleAsync(usuario.FirebaseUid, default);

        resultado.Should().BeSameAs(solicitud);
        solicitudes.Verify(r => r.BuscarUltimaPorUsuarioAsync(usuario.Id, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SinSolicitudes_DevuelveNulo()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = "uid-sin-solicitud" };
        var usuarios = new Mock<IUsuarioRepository>();
        var solicitudes = new Mock<ISolicitudRestauranteRepository>();
        usuarios.Setup(r => r.GetByFirebaseUidAsync(usuario.FirebaseUid, default)).ReturnsAsync(usuario);
        solicitudes.Setup(r => r.BuscarUltimaPorUsuarioAsync(usuario.Id, default))
            .ReturnsAsync((SolicitudRestaurante?)null);
        var casoDeUso = new ObtenerMiSolicitudRestauranteUseCase(solicitudes.Object, usuarios.Object);

        var resultado = await casoDeUso.HandleAsync(usuario.FirebaseUid, default);

        resultado.Should().BeNull();
    }
}
