namespace GustosApp.Application.Services
{
    public class GeminiSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int TimeoutSegundos { get; set; } = 12;
        public int RecomendacionesPorMinuto { get; set; } = 10;
        public int AnalisisPorMinuto { get; set; } = 5;
        public bool Habilitada => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Model);
    }
}
