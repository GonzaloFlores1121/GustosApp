using Google.GenAI;
using Google.GenAI.Types;
using GustosApp.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GustosApp.Application.Services;

/// <summary>Genera texto con Gemini según la configuración de la aplicación.</summary>
public sealed class RecomendacionAIService(
    IOptions<GeminiSettings> opciones,
    Func<HttpClient> crearHttpClient,
    ILogger<RecomendacionAIService> logger) : IRecomendacionAIService
{
    /// <inheritdoc />
    public async Task<string?> GenerarRecomendacion(string prompt, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var config = opciones.Value;
        if (!config.Habilitada)
            return null;

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(config.TimeoutSegundos, 1, 60)));
        try
        {
            using var cliente = new Client(enterprise: false, apiKey: config.ApiKey,
                clientOptions: new ClientOptions { HttpClientFactory = crearHttpClient },
                httpOptions: new HttpOptions
                {
                    BaseUrl = "https://generativelanguage.googleapis.com",
                    RetryOptions = new HttpRetryOptions { Attempts = 1 }
                });
            var respuesta = await cliente.Models.GenerateContentAsync(config.Model, prompt, cancellationToken: limite.Token);
            limite.Token.ThrowIfCancellationRequested();
            var salida = respuesta.Candidates?.FirstOrDefault();
            var texto = salida?.FinishReason == FinishReason.Stop
                ? string.Concat(salida.Content?.Parts?.Where(parte => parte.Thought != true)
                    .Select(parte => parte.Text) ?? [])
                : null;
            if (!string.IsNullOrWhiteSpace(texto))
                return texto;

            logger.LogWarning("Gemini devolvió una respuesta incompleta, bloqueada o sin texto.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Gemini superó el tiempo de espera.");
        }
        catch (HttpRequestException ex)
        {
            ct.ThrowIfCancellationRequested();
            logger.LogWarning("Falló Gemini. Tipo: {Tipo}. Código: {Codigo}",
                ex.GetType().Name, ex is ApiException api ? (int?)api.StatusCode : (int?)ex.StatusCode);
        }
        catch (System.Text.Json.JsonException)
        {
            logger.LogWarning("Gemini devolvió una respuesta que no se pudo interpretar.");
        }

        ct.ThrowIfCancellationRequested();
        return null;
    }
}
