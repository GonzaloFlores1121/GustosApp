namespace GustosApp.API.Extensiones
{
    public static class GeminiLoggingExtensions
    {
        public static void ValidateGeminiSettings(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();

            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            var apiKey = config["GeminiSettings:ApiKey"];
            var baseUrl = config["GeminiSettings:BaseUrl"];

            if (string.IsNullOrWhiteSpace(apiKey))
                logger.LogError("❌ GeminiSettings.ApiKey falta o está vacía");

            if (string.IsNullOrWhiteSpace(baseUrl))
                logger.LogError("❌ GeminiSettings.BaseUrl falta o está vacía");

            logger.LogInformation("GeminiSettings validados correctamente (sin mostrar valores sensibles).");
        }

    }
}
