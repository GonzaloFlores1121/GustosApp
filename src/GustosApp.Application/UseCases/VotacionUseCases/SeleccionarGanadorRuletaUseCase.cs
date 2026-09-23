using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;

namespace GustosApp.Application.UseCases.VotacionUseCases
{
    public class SeleccionarGanadorRuletaUseCase
    {
        private readonly IVotacionRepository _votacionRepository;
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly INotificacionesVotacionService _notificaciones;

        public SeleccionarGanadorRuletaUseCase(
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
            // 1. Verificar usuario
            var usuario = await _usuarioRepository.GetByFirebaseUidAsync(firebaseUid, ct)
                ?? throw new UnauthorizedAccessException("Usuario no encontrado");

            // 2. Cargar votación con candidatos
            var votacion = await _votacionRepository.ObtenerPorIdConCandidatosAsync(votacionId, ct)
                ?? throw new ArgumentException("Votación no encontrada");


            // 3. Validar que sea ADMIN DEL GRUPO
            var grupo = votacion.Grupo;
            if (grupo.AdministradorId != usuario.Id)
                throw new AccesoProhibidoException("Solo el administrador puede seleccionar el ganador");


            // 4. Validar estado de la votación
            if (votacion.Estado != EstadoVotacion.Activa)
                throw new InvalidOperationException("La votación no está activa");


            // 5. Validar que la ruleta solo se pueda usar si NO hay ya un ganador
            if (votacion.RestauranteGanadorId.HasValue)
                throw new InvalidOperationException("Ya se seleccionó un ganador para esta votación");

            if (!votacion.TodosLosParticipantesVotaron())
                throw new InvalidOperationException("La ruleta solo puede utilizarse cuando todos los participantes hayan votado");


            // 6. Validar empate real
            var restaurantesEmpatados = votacion.ObtenerRestaurantesEmpatados();
            if (restaurantesEmpatados.Count < 2)
                throw new InvalidOperationException("No hay empate en esta votación");


            // El servidor selecciona entre los empatados. El cliente solo anima el
            // resultado confirmado y no puede imponer un ganador.
            var candidatosOficiales = votacion.RestaurantesCandidatos
                .Select(c => c.RestauranteId)
                .ToHashSet();

            var opciones = restaurantesEmpatados
                .Where(candidatosOficiales.Contains)
                .ToList();

            if (opciones.Count != restaurantesEmpatados.Count)
                throw new InvalidOperationException("Hay restaurantes empatados que no son candidatos válidos");

            var restauranteGanadorId = opciones[RandomNumberGenerator.GetInt32(opciones.Count)];


            // 9. Asignar ganador y cerrar la votación
            votacion.CerrarVotacion(restauranteGanadorId);

            // Guardar antes de anunciar el resultado.
            await _votacionRepository.ActualizarVotacionAsync(votacion, ct);

            await _notificaciones.NotificarGanador(votacion.GrupoId, votacion.Id, restauranteGanadorId);
            await _notificaciones.NotificarVotacionCerrada(
                votacion.GrupoId,
                votacion.Id,
                restauranteGanadorId);

            return votacion;
        }
    }
    }
