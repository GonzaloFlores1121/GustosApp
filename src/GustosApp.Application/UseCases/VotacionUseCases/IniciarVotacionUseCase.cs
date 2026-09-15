using System;
using System.Threading;
using System.Threading.Tasks;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;

namespace GustosApp.Application.UseCases.VotacionUseCases
{
    public class IniciarVotacionUseCase
    {
        private readonly IVotacionRepository _votacionRepository;
        private readonly IGrupoRepository _grupoRepository;
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly INotificacionesVotacionService _notificadorVotaciones;

        public IniciarVotacionUseCase(
            IVotacionRepository votacionRepository,
            IGrupoRepository grupoRepository,
            IUsuarioRepository usuarioRepository,
           INotificacionesVotacionService notificadorVotaciones)
        {
            _votacionRepository = votacionRepository;
            _grupoRepository = grupoRepository;
            _usuarioRepository = usuarioRepository;
            _notificadorVotaciones = notificadorVotaciones;
        }

        public async Task<VotacionGrupo> HandleAsync(
         string firebaseUid,
         Guid grupoId,
         string? descripcion,
         List<Guid> restaurantesCandidatos,
         CancellationToken ct = default)
        {
            // 1. Verificar que el usuario existe
            var usuario = await _usuarioRepository.GetByFirebaseUidAsync(firebaseUid, ct)
                ?? throw new UnauthorizedAccessException("Usuario no encontrado");

            // 2. Validar que el grupo exista y que el usuario sea su administrador
            var grupo = await _grupoRepository.GetByIdAsync(grupoId, ct)
                ?? throw new NotFoundException("Grupo no encontrado.");

            if (grupo.AdministradorId != usuario.Id)
                throw new AccesoProhibidoException("Solo el administrador del grupo puede iniciar una votación.");

            // 3. No permitir dos votaciones simultáneas
            var votacionActiva = await _votacionRepository.ObtenerVotacionActivaAsync(grupoId, ct);
            if (votacionActiva != null)
                throw new InvalidOperationException("Ya existe una votación activa en este grupo");

            if (restaurantesCandidatos == null || restaurantesCandidatos.Count == 0)
                throw new InvalidOperationException("Debe seleccionar al menos un restaurante candidato.");

            var hayParticipantes = grupo.Miembros.Any(m => m.Activo && m.afectarRecomendacion);
            if (!hayParticipantes)
                throw new InvalidOperationException("Debe seleccionar al menos un miembro activo para participar de la votación.");


            // 4. Crear votación
            var votacion = new VotacionGrupo(grupoId, descripcion);

            // 5. Agregar restaurantes candidatos
            foreach (var restauranteId in restaurantesCandidatos)
            {
                votacion.RestaurantesCandidatos.Add(
                    new VotacionRestaurante(votacion.Id, restauranteId)
                );
            }

            // 6. Guardar votación completa
            await _votacionRepository.CrearVotacionAsync(votacion, ct);
            // 7. Notificar inicio de votación
            await _notificadorVotaciones.NotificarVotacionIniciada(votacion);
            return votacion;
        }
    }
    }
