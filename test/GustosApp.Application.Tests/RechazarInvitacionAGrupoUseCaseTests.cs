using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.GrupoUseCases.InvitacionGrupoUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;

namespace GustosApp.Application.Tests;

public class RechazarInvitacionAGrupoUseCaseTests
{
    private readonly Mock<IUsuarioRepository> _usuarios = new();
    private readonly Mock<IInvitacionGrupoRepository> _invitaciones = new();
    private readonly Mock<ISolicitudAmistadRealtimeService> _realtime = new();

    private RechazarInvitacionAGrupoUseCase CreateSut() =>
        new(_usuarios.Object, _invitaciones.Object, _realtime.Object);

    [Fact]
    public async Task HandleAsync_RechazaPorIdDeInvitacion()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = "uid" };
        var invitacion = new InvitacionGrupo(Guid.NewGuid(), usuario.Id, Guid.NewGuid());
        _usuarios.Setup(r => r.GetByFirebaseUidAsync("uid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _invitaciones.Setup(r => r.GetByIdAsync(invitacion.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitacion);

        await CreateSut().HandleAsync("uid", invitacion.Id, CancellationToken.None);

        Assert.Equal(EstadoInvitacion.Rechazada, invitacion.Estado);
        _invitaciones.Verify(r => r.UpdateAsync(invitacion, It.IsAny<CancellationToken>()), Times.Once);
        _realtime.Verify(r => r.EnviarSolicitudResueltaAsync(
            "uid", invitacion.Id, "invitacion_grupo", "rechazada", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DeOtroDestinatario_LanzaAccesoProhibido()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = "uid" };
        var invitacion = new InvitacionGrupo(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _usuarios.Setup(r => r.GetByFirebaseUidAsync("uid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _invitaciones.Setup(r => r.GetByIdAsync(invitacion.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitacion);

        await Assert.ThrowsAsync<AccesoProhibidoException>(() =>
            CreateSut().HandleAsync("uid", invitacion.Id, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_Expirada_PersisteEstadoYNoLaResuelve()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = "uid" };
        var invitacion = new InvitacionGrupo(
            Guid.NewGuid(), usuario.Id, Guid.NewGuid(), fechaCreacionUtc: DateTime.UtcNow.AddDays(-8));
        _usuarios.Setup(r => r.GetByFirebaseUidAsync("uid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _invitaciones.Setup(r => r.GetByIdAsync(invitacion.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitacion);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateSut().HandleAsync("uid", invitacion.Id, CancellationToken.None));

        Assert.Equal(EstadoInvitacion.Expirada, invitacion.Estado);
        _invitaciones.Verify(r => r.UpdateAsync(invitacion, It.IsAny<CancellationToken>()), Times.Once);
    }
}
