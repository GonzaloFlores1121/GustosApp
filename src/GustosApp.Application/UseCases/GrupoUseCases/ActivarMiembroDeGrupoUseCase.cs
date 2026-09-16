using GustosApp.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Application.Common.Exceptions;

namespace GustosApp.Application.UseCases.GrupoUseCases
{
    public class ActivarMiembroDeGrupoUseCase
    {

        private IGrupoRepository _grupoRepository;
        private IUsuarioRepository _usuarioRepository;
        private IMiembroGrupoRepository _miembroGrupoRepository;
        private readonly IVotacionRepository _votacionRepository;
        public ActivarMiembroDeGrupoUseCase(
            IGrupoRepository grupo,
            IUsuarioRepository usuarioRepository,
            IMiembroGrupoRepository miembroGrupoRepository,
           IVotacionRepository votacionRepository)
        {
            _grupoRepository = grupo;
            _usuarioRepository = usuarioRepository;
            _miembroGrupoRepository = miembroGrupoRepository;
            _votacionRepository = votacionRepository;

        }

        public async Task<bool> Handle(Guid grupoId, Guid usuarioId, string firebaseUid)
        {
            var votacionActiva = await _votacionRepository.ObtenerVotacionActivaAsync(grupoId);
            if (votacionActiva != null)
                throw new InvalidOperationException("No se pueden modificar los miembros mientras haya una votación activa.");

            var usuarioSolicitante = await _usuarioRepository.GetByFirebaseUidAsync(firebaseUid);
            var usuarioObtenido = await _usuarioRepository.GetByIdAsync(usuarioId);
            if (usuarioSolicitante == null)
            {
                throw new UnauthorizedAccessException("El usuario solicitante no existe.");
            }
            if (usuarioObtenido == null)
            {
                throw new ArgumentException("El ID de usuario a activar no existe.", nameof(usuarioId));
            }

            if (await _grupoRepository.GetByIdAsync(grupoId)==null)
            {
                throw new KeyNotFoundException("EL grupo no existe");
            }

            var esAdmin = await _grupoRepository.UsuarioEsAdministradorAsync(grupoId, usuarioSolicitante.Id);

            if (!esAdmin)
            {
                throw new AccesoProhibidoException("Solo el administrador del grupo puede incluir miembros en la recomendación.");
            }

            var miembroGrupo = await _miembroGrupoRepository.GetByGrupoYUsuarioAsync(grupoId, usuarioObtenido.IdUsuario);

            if (miembroGrupo == null)
            {
                throw new InvalidOperationException("El usuario no es miembro del grupo.");
            }

            // La operación es idempotente cuando el miembro ya participa de la recomendación.
            if (miembroGrupo.ParticipaEnRecomendacion)
            {
                return true;
            }

            // Incluir las preferencias del miembro en la próxima recomendación.
            return await _miembroGrupoRepository.ActivarMiembro(grupoId, usuarioObtenido.Id);
        }
    }
}
