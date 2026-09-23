using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;

namespace GustosApp.Application.UseCases.VotacionUseCases
{
    public class RegistrarVotoUseCase
    {
        private readonly IVotacionRepository _votacionRepository;
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IGrupoRepository _grupoRepository;
        private readonly IRestauranteRepository _restauranteRepository;
        private readonly INotificacionesVotacionService _notificaciones;

        public RegistrarVotoUseCase(
            IVotacionRepository votacionRepository,
            IUsuarioRepository usuarioRepository,
            IGrupoRepository grupoRepository,
            IRestauranteRepository restauranteRepository,
           INotificacionesVotacionService notificaciones)
        {
            _votacionRepository = votacionRepository;
            _usuarioRepository = usuarioRepository;
            _grupoRepository = grupoRepository;
            _restauranteRepository = restauranteRepository;
            _notificaciones = notificaciones;
        }

        public async Task<VotoRestaurante> HandleAsync(
      string firebaseUid,
      Guid votacionId,
      Guid restauranteId,
      string? comentario = null,
      CancellationToken ct = default)
        {
            // 1. Obtener usuario
            var usuario = await _usuarioRepository.GetByFirebaseUidAsync(firebaseUid, ct)
                ?? throw new UnauthorizedAccessException("Usuario no encontrado");

            // 2. Obtener votación con CANDIDATOS
            var votacion = await _votacionRepository.ObtenerPorIdConCandidatosAsync(votacionId, ct)
                ?? throw new ArgumentException("Votación no encontrada");

            // 3. Verificar estado activo
            if (votacion.Estado != EstadoVotacion.Activa)
                throw new InvalidOperationException("La votación no está activa");

            // 4. Verificar la fotografía de participantes tomada al iniciar
            if (!votacion.Participantes.Any(p => p.UsuarioId == usuario.Id))
                throw new AccesoProhibidoException("No estás incluido entre los participantes de esta votación.");

            // 5. VALIDAR CANDIDATO
            bool esCandidato = votacion.RestaurantesCandidatos.Any(rc => rc.RestauranteId == restauranteId);
            if (!esCandidato)
                throw new InvalidOperationException("Este restaurante no es candidato en esta votación.");

            // 6. Obtener restaurante
            var restaurante = await _restauranteRepository.GetRestauranteByIdAsync(restauranteId, ct)
                ?? throw new ArgumentException("Restaurante no encontrado");

            // 7. ¿Ya votó?
            var votoExistente = await _votacionRepository.ObtenerVotoUsuarioAsync(votacionId, usuario.Id, ct);

            if (votoExistente != null)
            {
                // --- ACTUALIZAR VOTO ---
                votoExistente.ActualizarVoto(restauranteId, comentario);
                await _votacionRepository.ActualizarVotoAsync(votoExistente, ct);

                var payloadUpdate = new EventoVotoRegistrado
                {
                    VotacionId = votacionId,
                    UsuarioId = usuario.Id,
                    UsuarioNombre = usuario.Nombre,
                    UsuarioFirebaseUid = usuario?.FirebaseUid,
                    UsuarioFoto = usuario.FotoPerfilUrl ?? "",
                    RestauranteId = restaurante.Id,
                    RestauranteNombre = restaurante.Nombre,
                    RestauranteImagen = restaurante.ImagenUrl ?? "",
                    EsActualizacion = true
                };

                await FinalizarYNotificarAsync(votacion, payloadUpdate, ct);

                return votoExistente;
            }

            // --- CREAR NUEVO VOTO ---
            var nuevoVoto = new VotoRestaurante(votacionId, usuario.Id, restauranteId, comentario);
            votacion.Votos.Add(nuevoVoto);
            await _votacionRepository.RegistrarVotoAsync(nuevoVoto, ct);

            var payloadNuevo = new EventoVotoRegistrado
            {
                VotacionId = votacionId,
                UsuarioId = usuario.Id,
                UsuarioNombre = usuario.Nombre,
                UsuarioFoto = usuario.FotoPerfilUrl ?? "",
                RestauranteId = restaurante.Id,
                RestauranteNombre = restaurante.Nombre,
                RestauranteImagen = restaurante.ImagenUrl ?? "",
                EsActualizacion = false
            };

            await FinalizarYNotificarAsync(votacion, payloadNuevo, ct);

            return nuevoVoto;
        }

        private async Task FinalizarYNotificarAsync(
            VotacionGrupo votacion,
            EventoVotoRegistrado evento,
            CancellationToken ct)
        {
            var seCerro = votacion.IntentarCerrarConGanadorUnico();

            if (seCerro)
                await _votacionRepository.ActualizarVotacionAsync(votacion, ct);

            await _notificaciones.NotificarVotoRegistrado(votacion.GrupoId, evento);
            await _notificaciones.NotificarResultadosActualizados(votacion.GrupoId, votacion.Id);

            if (seCerro && votacion.RestauranteGanadorId.HasValue)
            {
                await _notificaciones.NotificarGanador(
                    votacion.GrupoId,
                    votacion.Id,
                    votacion.RestauranteGanadorId.Value);

                await _notificaciones.NotificarVotacionCerrada(
                    votacion.GrupoId,
                    votacion.Id,
                    votacion.RestauranteGanadorId);
            }
            else if (votacion.TodosLosParticipantesVotaron())
            {
                await _notificaciones.NotificarEmpate(votacion.GrupoId, votacion.Id);
            }
        }

    }
}
