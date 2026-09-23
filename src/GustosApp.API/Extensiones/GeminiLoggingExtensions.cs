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
            var model = config["GeminiSettings:Model"];

            if (string.IsNullOrWhiteSpace(apiKey))
                logger.LogError("❌ GeminiSettings.ApiKey falta o está vacía");

            if (string.IsNullOrWhiteSpace(model))
                logger.LogError("❌ GeminiSettings.Model falta o está vacío");
            else
                logger.LogInformation("Gemini configurado con el modelo {Modelo} (sin mostrar valores sensibles).", model);
        }

    }
}
