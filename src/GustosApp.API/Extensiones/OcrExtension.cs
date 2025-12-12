using GustosApp.Application.Interfaces;
using GustosApp.Infraestructure.Ocr;

namespace GustosApp.API.Extensiones
{
    public static class OcrExtension
    {
        public static IServiceCollection AgregarOcrService(this IServiceCollection services,
            IConfiguration config)
        {
            services.AddSingleton<IOcrService>(sp =>
            {
                var env = sp.GetRequiredService<IWebHostEnvironment>();
                var logger = sp.GetRequiredService<ILogger<IOcrService>>();

                logger.LogInformation("Inicializando Google Vision OCR...");

                // 1) PRODUCCIÓN VIA BASE64
                var base64 = config["GOOGLE_VISION_KEY_BASE64"];

                if (!string.IsNullOrWhiteSpace(base64))
                {
                    try
                    {
                        logger.LogInformation("Usando GOOGLE_VISION_KEY_BASE64...");

                        var bytes = Convert.FromBase64String(base64);
                        var jsonString = System.Text.Encoding.UTF8.GetString(bytes);

                        logger.LogInformation("Credencial Base64 decodificada correctamente.");

                        return new GoogleVisionOcrService(jsonString);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error decodificando GOOGLE_VISION_KEY_BASE64.");
                    }
                }
                else
                {
                    logger.LogWarning("GOOGLE_VISION_KEY_BASE64 no está configurado.");
                }

                // 2) LOCAL VIA ARCHIVO
                var localPath = Path.Combine(env.ContentRootPath, "secrets", "google-vision.json");
                logger.LogInformation("Intentando cargar credencial local: {Path}", localPath);

                if (!File.Exists(localPath))
                {
                    logger.LogError("No existe google-vision.json en: {Path}", localPath);
                    throw new FileNotFoundException("No se encontró google-vision.json en /secrets", localPath);
                }

                var jsonFile = File.ReadAllText(localPath);
                logger.LogInformation("Archivo local cargado correctamente.");

                return new GoogleVisionOcrService(jsonFile);
            });


            return services;
        }
    }
}
