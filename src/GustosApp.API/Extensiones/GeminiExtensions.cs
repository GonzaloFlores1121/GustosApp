using GustosApp.Application.Interfaces;
using GustosApp.Application.Services;
using Microsoft.Extensions.Options;

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
        services.AddOptions<GeminiSettings>()
            .Bind(config.GetSection("GeminiSettings"))
            .PostConfigure(opciones =>
            {
                if (string.IsNullOrWhiteSpace(opciones.ApiKey))
                    opciones.ApiKey = config["GOOGLE_API_KEY"] ?? string.Empty;
            });

        // Registrar HttpClient + Servicio
        const string nombreCliente = nameof(IRecomendacionAIService);
        services.AddHttpClient(nombreCliente)
            .ConfigureHttpClient(http => http.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .RedactLoggedHeaders(["x-goog-api-key", "Authorization"]);
        services.AddSingleton<IRecomendacionAIService>(sp =>
        {
            var fabrica = sp.GetRequiredService<IHttpClientFactory>();
            return new RecomendacionAIService(sp.GetRequiredService<IOptions<GeminiSettings>>(),
                () => fabrica.CreateClient(nombreCliente), sp.GetRequiredService<ILogger<RecomendacionAIService>>());
        });

  

        return services;
    }
    }
}
