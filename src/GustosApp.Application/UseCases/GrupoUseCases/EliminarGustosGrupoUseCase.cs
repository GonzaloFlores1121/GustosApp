using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GustosApp.Application.UseCases.GrupoUseCases
{
    public class EliminarGustosGrupoUseCase
    {
        private IGrupoRepository _grupoRepository;
        private IGustoRepository _gustoRepository;
        private IGustosGrupoRepository _gustosGrupoRepository;
        private IUsuarioRepository _usuarioRepository;
        private IMiembroGrupoRepository _miembroGrupoRepository;

        public EliminarGustosGrupoUseCase(
            IGrupoRepository grupoRepository,
            IGustoRepository gustoRepository,
            IGustosGrupoRepository gustosGrupoRepository,
            IUsuarioRepository usuarioRepository,
            IMiembroGrupoRepository miembroGrupoRepository)
        {
            _grupoRepository = grupoRepository;
            _gustoRepository = gustoRepository;
            _gustosGrupoRepository = gustosGrupoRepository;
            _usuarioRepository = usuarioRepository;
            _miembroGrupoRepository = miembroGrupoRepository;
        }

        public async Task<bool> Handle(List<string> gustosDeUsuario, Guid grupoId,string firebaseUid)
        {
            var miembroGrupo = await _usuarioRepository.GetByFirebaseUidAsync(firebaseUid);

            if (miembroGrupo == null)
            {
                throw new UnauthorizedAccessException("El usuario no existe.");
            }

            if (!await _grupoRepository.ExistsAsync(grupoId))
            {
                throw new KeyNotFoundException("el grupo no existe.");
            }

            if (!await _miembroGrupoRepository.UsuarioEsMiembroActivoAsync(grupoId, miembroGrupo.Id))
            {
                throw new UnauthorizedAccessException("El miembro no es un usuario activo (expulsado)");
            }

            throw new GustosApp.Application.Common.Exceptions.PreferenciasGrupoDesdePerfilException();
        }
    }
}
