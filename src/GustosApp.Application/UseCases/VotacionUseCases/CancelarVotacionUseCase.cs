using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;

namespace GustosApp.Application.UseCases.VotacionUseCases;

public class CancelarVotacionUseCase
{
    private readonly IVotacionRepository _votacionRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly INotificacionesVotacionService _notificaciones;

    public CancelarVotacionUseCase(
        IVotacionRepository votacionRepository,
        IUsuarioRepository usuarioRepository,
        INotificacionesVotacionService notificaciones)
    {
        _votacionRepository = votacionRepository;
        _usuarioRepository = usuarioRepository;
        _notificaciones = notificaciones;
    }

    public async Task<VotacionGrupo> HandleAsync(
        string firebaseUid,
        Guid votacionId,
        CancellationToken ct = default)
    {
        var usuario = await _usuarioRepository.GetByFirebaseUidAsync(firebaseUid, ct)
            ?? throw new UnauthorizedAccessException("Usuario no encontrado");

        var votacion = await _votacionRepository.ObtenerPorIdConCandidatosAsync(votacionId, ct)
            ?? throw new NotFoundException("Votación no encontrada.");

        if (votacion.Grupo.AdministradorId != usuario.Id)
            throw new AccesoProhibidoException("Solo el administrador puede cancelar la votación.");

        votacion.Cancelar();
        await _votacionRepository.ActualizarVotacionAsync(votacion, ct);
        await _notificaciones.NotificarVotacionCancelada(votacion.GrupoId, votacion.Id);

        return votacion;
    }
}
