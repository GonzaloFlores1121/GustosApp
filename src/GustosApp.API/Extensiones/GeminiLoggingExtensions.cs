namespace GustosApp.API.Extensiones
{
    public static class GeminiLoggingExtensions
    {
        public static void ValidateGeminiSettings(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();

            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            var opciones = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GustosApp.Application.Services.GeminiSettings>>().Value;
            if (!opciones.Habilitada)
                logger.LogInformation("Gemini sin configurar. Se usarán las coincidencias locales.");
            else
                logger.LogInformation("Gemini configurado.");
        }

    }
}
