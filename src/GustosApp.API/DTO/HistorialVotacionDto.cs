namespace GustosApp.API.DTO
{
    public class HistorialVotacionesResponse
    {
        public int Pagina { get; set; }
        public int TamanoPagina { get; set; }
        public int Total { get; set; }
        public int TotalPaginas { get; set; }
        public List<VotacionHistorialResponse> Votaciones { get; set; } = new();
    }

    public class VotacionHistorialResponse
    {
        public Guid VotacionId { get; set; }
        public string? Descripcion { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaCierre { get; set; }
        public int CantidadParticipantes { get; set; }
        public int CantidadVotos { get; set; }
        public RestauranteGanadorHistorialResponse? Ganador { get; set; }
    }

    public class RestauranteGanadorHistorialResponse
    {
        public Guid RestauranteId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string? ImagenUrl { get; set; }
    }
}
