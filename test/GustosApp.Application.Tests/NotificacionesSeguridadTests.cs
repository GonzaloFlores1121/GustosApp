using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.NotificacionUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using Moq;

namespace GustosApp.Application.Tests;

public class NotificacionesSeguridadTests
{
    [Fact]
    public async Task MarcarLeida_NotificacionAjena_EsRechazada()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = "uid" };
        var notificacion = new Notificacion
        {
            Id = Guid.NewGuid(),
            UsuarioDestinoId = Guid.NewGuid()
        };
        var repositorio = new Mock<INotificacionRepository>();
        var usuarios = new Mock<IUsuarioRepository>();
        usuarios.Setup(r => r.GetByFirebaseUidAsync("uid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        repositorio.Setup(r => r.GetByIdAsync(notificacion.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notificacion);
        var sut = new MarcarNotificacionLeidaUseCase(repositorio.Object, usuarios.Object);

        await Assert.ThrowsAsync<AccesoProhibidoException>(() =>
            sut.HandleAsync("uid", notificacion.Id, CancellationToken.None));

        repositorio.Verify(r => r.MarcarComoLeidaAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Publicar_FallaRealtime_ConservaNotificacionPersistida()
    {
        var destinatario = new Usuario
        {
            Id = Guid.NewGuid(),
            FirebaseUid = "uid"
        };
        var repositorio = new Mock<INotificacionRepository>();
        var realtime = new Mock<INotificacionRealtimeService>();
        repositorio.Setup(r => r.CrearSiNoExisteAsync(
                It.IsAny<Notificacion>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notificacion n, CancellationToken _) => n);
        realtime.Setup(r => r.EnviarNotificacionAsync(
                destinatario.FirebaseUid,
                It.IsAny<NotificacionRealtimeMensaje>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR no disponible"));
        var sut = new PublicarNotificacionInformativaUseCase(repositorio.Object, realtime.Object);

        var result = await sut.HandleAsync(
            destinatario,
            "Aprobada",
            "Tu solicitud fue aprobada",
            TipoNotificacion.SolicitudRestauranteAprobada,
            "solicitud:1:aprobada",
            "/restaurante/1/dashboard",
            CancellationToken.None);

        Assert.Equal("solicitud:1:aprobada", result.ClaveUnica);
        repositorio.Verify(r => r.CrearSiNoExisteAsync(
            It.IsAny<Notificacion>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
