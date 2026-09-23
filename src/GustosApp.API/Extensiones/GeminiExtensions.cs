using GustosApp.Application.Interfaces;
using GustosApp.Application.Services;

namespace GustosApp.API.Extensiones
{
    public static class GeminiExtensions
    {
        public static IServiceCollection AgregarGeminiIntegracion(
        this IServiceCollection services,
        IConfiguration config
        )
    {
        // Registrar Settings 
        services.Configure<GeminiSettings>(config.GetSection("GeminiSettings"));

        // Registrar HttpClient + Servicio
        services.AddHttpClient<IRecomendacionAIService, RecomendacionAIService>();

  

        return services;
    }
    }
}
