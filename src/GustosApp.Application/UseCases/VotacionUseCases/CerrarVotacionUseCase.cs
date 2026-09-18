using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;

namespace GustosApp.Application.UseCases.VotacionUseCases
{
    public class CerrarVotacionUseCase
    {
        private readonly IVotacionRepository _votacionRepository;
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly INotificacionesVotacionService _notificaciones;

        public CerrarVotacionUseCase(
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
      Guid? restauranteGanadorId = null,
      CancellationToken ct = default)
        {
            // 1. Validar usuario
            var usuario = await _usuarioRepository.GetByFirebaseUidAsync(firebaseUid, ct)
                ?? throw new UnauthorizedAccessException("Usuario no encontrado");

            // 2. Obtener votación con candidatos
            var votacion = await _votacionRepository.ObtenerPorIdConCandidatosAsync(votacionId, ct)
                ?? throw new ArgumentException("Votación no encontrada");

            var grupo = votacion.Grupo;

            // 3. Validar que sea administrador
            if (grupo.AdministradorId != usuario.Id)
                throw new AccesoProhibidoException("Solo el administrador puede cerrar la votación.");

            if (votacion.Estado != EstadoVotacion.Activa)
                throw new InvalidOperationException("La votación no está activa");

            if (!votacion.TodosLosParticipantesVotaron())
                throw new InvalidOperationException("La votación no puede cerrarse hasta que todos los participantes hayan votado");

            var ganadores = votacion.ObtenerRestaurantesEmpatados();
            if (ganadores.Count != 1)
                throw new InvalidOperationException("La votación está empatada y debe resolverse mediante la ruleta");

            var ganadorCalculado = ganadores.Single();

            // Si se informa un ganador, debe coincidir con el resultado de los votos.
            if (restauranteGanadorId.HasValue)
            {
                var esCandidato = votacion.RestaurantesCandidatos
                    .Any(rc => rc.RestauranteId == restauranteGanadorId);

                if (!esCandidato)
                    throw new InvalidOperationException("El ganador debe ser un restaurante candidato");

                if (restauranteGanadorId.Value != ganadorCalculado)
                    throw new InvalidOperationException("El ganador informado no coincide con el resultado de la votación");
            }

            votacion.CerrarVotacion(ganadorCalculado);

            await _votacionRepository.ActualizarVotacionAsync(votacion, ct);

            await _notificaciones.NotificarVotacionCerrada(votacion.GrupoId, votacion.Id, ganadorCalculado);


            return votacion;
        }

    }
    }
