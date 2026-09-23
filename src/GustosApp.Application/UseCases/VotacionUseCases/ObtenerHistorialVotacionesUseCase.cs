using GustosApp.Application.Common.Exceptions;
using GustosApp.Domain.Interfaces;

namespace GustosApp.Application.UseCases.VotacionUseCases
{
    public class ObtenerHistorialVotacionesUseCase
    {
        private const int TamanoPaginaMaximo = 50;

        private readonly IVotacionRepository _votacionRepository;
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IGrupoRepository _grupoRepository;

        public ObtenerHistorialVotacionesUseCase(
            IVotacionRepository votacionRepository,
            IUsuarioRepository usuarioRepository,
            IGrupoRepository grupoRepository)
        {
            _votacionRepository = votacionRepository;
            _usuarioRepository = usuarioRepository;
            _grupoRepository = grupoRepository;
        }

        public async Task<HistorialVotacionesResultado> HandleAsync(
            string firebaseUid,
            Guid grupoId,
            int pagina = 1,
            int tamanoPagina = 10,
            CancellationToken ct = default)
        {
            if (pagina < 1)
                throw new ArgumentOutOfRangeException(nameof(pagina), "La página debe ser mayor o igual a uno.");

            if (tamanoPagina < 1 || tamanoPagina > TamanoPaginaMaximo)
                throw new ArgumentOutOfRangeException(
                    nameof(tamanoPagina),
                    $"El tamaño de página debe estar entre 1 y {TamanoPaginaMaximo}.");

            var usuario = await _usuarioRepository.GetByFirebaseUidAsync(firebaseUid, ct)
                ?? throw new UnauthorizedAccessException("Usuario no encontrado");

            var grupo = await _grupoRepository.GetByIdAsync(grupoId, ct)
                ?? throw new ArgumentException("Grupo no encontrado");

            var esAdministrador = grupo.AdministradorId == usuario.Id;
            var esMiembroActivo = grupo.Miembros.Any(m => m.UsuarioId == usuario.Id && m.Activo);

            if (!esAdministrador && !esMiembroActivo)
                throw new AccesoProhibidoException("No eres un miembro activo del grupo.");

            var (votaciones, total) = await _votacionRepository.ObtenerHistorialVotacionesAsync(
                grupoId,
                pagina,
                tamanoPagina,
                ct);

            return new HistorialVotacionesResultado
            {
                Pagina = pagina,
                TamanoPagina = tamanoPagina,
                Total = total,
                TotalPaginas = total == 0
                    ? 0
                    : (int)Math.Ceiling(total / (double)tamanoPagina),
                Votaciones = votaciones.Select(v => new VotacionHistorial
                {
                    VotacionId = v.Id,
                    Descripcion = v.Descripcion,
                    FechaInicio = v.FechaInicio,
                    FechaCierre = v.FechaCierre,
                    CantidadParticipantes = v.Participantes.Count,
                    CantidadVotos = v.Votos.Count,
                    Ganador = v.RestauranteGanadorId.HasValue
                        ? new RestauranteGanadorHistorial
                        {
                            RestauranteId = v.RestauranteGanadorId.Value,
                            Nombre = v.RestauranteGanador?.Nombre ?? "Restaurante no disponible",
                            Direccion = v.RestauranteGanador?.Direccion ?? string.Empty,
                            ImagenUrl = v.RestauranteGanador?.ImagenUrl
                        }
                        : null
                }).ToList()
            };
        }
    }

    public class HistorialVotacionesResultado
    {
        public int Pagina { get; set; }
        public int TamanoPagina { get; set; }
        public int Total { get; set; }
        public int TotalPaginas { get; set; }
        public List<VotacionHistorial> Votaciones { get; set; } = new();
    }

    public class VotacionHistorial
    {
        public Guid VotacionId { get; set; }
        public string? Descripcion { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaCierre { get; set; }
        public int CantidadParticipantes { get; set; }
        public int CantidadVotos { get; set; }
        public RestauranteGanadorHistorial? Ganador { get; set; }
    }

    public class RestauranteGanadorHistorial
    {
        public Guid RestauranteId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string? ImagenUrl { get; set; }
    }
}
