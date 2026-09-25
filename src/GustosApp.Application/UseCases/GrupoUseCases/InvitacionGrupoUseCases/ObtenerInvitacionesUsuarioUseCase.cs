using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;

namespace GustosApp.Application.UseCases.GrupoUseCases.InvitacionGrupoUseCases
{
    public class ObtenerInvitacionesUsuarioUseCase
    {
        private readonly IInvitacionGrupoRepository _invitacionRepository;
        private readonly IUsuarioRepository _usuarioRepository;

        public ObtenerInvitacionesUsuarioUseCase(IInvitacionGrupoRepository invitacionRepository, IUsuarioRepository usuarioRepository)
        {
            _invitacionRepository = invitacionRepository;
            _usuarioRepository = usuarioRepository;
        }

        public async Task<IEnumerable<InvitacionGrupo>> HandleAsync(string firebaseUid, CancellationToken cancellationToken = default)
        {
            // Obtener el usuario
            var usuario = await _usuarioRepository.GetByFirebaseUidAsync(firebaseUid, cancellationToken);
            if (usuario == null)
                throw new UnauthorizedAccessException("Usuario no encontrado");

            await _invitacionRepository.MarcarInvitacionesExpiradasAsync(cancellationToken);
            var invitaciones = await _invitacionRepository.GetInvitacionesPendientesByUsuarioIdAsync(usuario.Id, cancellationToken);

            return invitaciones;
        }
    }
}
