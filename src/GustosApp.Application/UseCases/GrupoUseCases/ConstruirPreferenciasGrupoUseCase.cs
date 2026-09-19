using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;

namespace GustosApp.Application.UseCases.GrupoUseCases
{
    public class ConstruirPreferenciasGrupoUseCase
    {
        private readonly IGustosGrupoRepository _gustosGrupoRepository;
        private readonly IMiembroGrupoRepository _miembroGrupoRepository;
        private readonly IUsuarioRepository _usuarioRepository;

        public ConstruirPreferenciasGrupoUseCase(
            IGustosGrupoRepository gustosGrupoRepository,
            IMiembroGrupoRepository miembroGrupoRepository,
            IUsuarioRepository usuarioRepository)
        {
            _gustosGrupoRepository = gustosGrupoRepository;
            _miembroGrupoRepository = miembroGrupoRepository;
            _usuarioRepository = usuarioRepository;
        }

        public async Task<UsuarioPreferencias> HandleAsync(
            string firebaseUid,
            Guid grupoId,
            CancellationToken ct)
        {
            var usuario = await _usuarioRepository.GetByFirebaseUidAsync(firebaseUid, ct)
                ?? throw new UnauthorizedAccessException("Usuario no encontrado.");

            var esMiembroActivo = await _miembroGrupoRepository
                .UsuarioEsMiembroActivoAsync(grupoId, usuario.Id, ct);

            if (!esMiembroActivo)
                throw new UnauthorizedAccessException("El usuario no es miembro activo del grupo.");

            var gustos = await _gustosGrupoRepository.ObtenerGustosDelGrupo(grupoId);
            var preferenciasMiembros = await _miembroGrupoRepository
                .obtenerMiembrosActivosConSusPreferenciasYCondiciones(grupoId);

            return new UsuarioPreferencias
            {
                Gustos = gustos.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Restricciones = preferenciasMiembros.Restricciones
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                CondicionesMedicas = preferenciasMiembros.CondicionesMedicas
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }
    }
}
