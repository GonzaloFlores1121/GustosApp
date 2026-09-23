using FluentAssertions;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.VotacionUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;

namespace GustosApp.Application.Tests;

public class CancelarVotacionUseCaseTests
{
    private readonly Mock<IVotacionRepository> _votaciones = new();
    private readonly Mock<IUsuarioRepository> _usuarios = new();
    private readonly Mock<INotificacionesVotacionService> _notificaciones = new();

    private CancelarVotacionUseCase CrearCasoDeUso() => new(
        _votaciones.Object,
        _usuarios.Object,
        _notificaciones.Object);

    [Fact]
    public async Task HandleAsync_AdministradorCancelaSinGanadorYNotificaDespuesDeGuardar()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = "admin" };
        var grupo = new Grupo("Grupo", usuario.Id);
        var votacion = new VotacionGrupo(grupo.Id) { Grupo = grupo };
        var secuencia = new MockSequence();

        _usuarios.Setup(r => r.GetByFirebaseUidAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _votaciones.Setup(r => r.ObtenerPorIdConCandidatosAsync(votacion.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(votacion);
        _votaciones.InSequence(secuencia)
            .Setup(r => r.ActualizarVotacionAsync(votacion, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notificaciones.InSequence(secuencia)
            .Setup(r => r.NotificarVotacionCancelada(grupo.Id, votacion.Id))
            .Returns(Task.CompletedTask);

        var resultado = await CrearCasoDeUso().HandleAsync("admin", votacion.Id);

        resultado.Estado.Should().Be(EstadoVotacion.Cancelada);
        resultado.RestauranteGanadorId.Should().BeNull();
        resultado.FechaCierre.Should().NotBeNull();
        _notificaciones.Verify(
            r => r.NotificarVotacionCancelada(grupo.Id, votacion.Id),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MiembroNoAdministrador_NoCancela()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), FirebaseUid = "miembro" };
        var grupo = new Grupo("Grupo", Guid.NewGuid());
        var votacion = new VotacionGrupo(grupo.Id) { Grupo = grupo };

        _usuarios.Setup(r => r.GetByFirebaseUidAsync("miembro", It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _votaciones.Setup(r => r.ObtenerPorIdConCandidatosAsync(votacion.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(votacion);

        var accion = () => CrearCasoDeUso().HandleAsync("miembro", votacion.Id);

        await accion.Should().ThrowAsync<AccesoProhibidoException>();
        votacion.Estado.Should().Be(EstadoVotacion.Activa);
        _votaciones.Verify(
            r => r.ActualizarVotacionAsync(It.IsAny<VotacionGrupo>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
