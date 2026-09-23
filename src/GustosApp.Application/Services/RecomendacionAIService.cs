using Google.GenAI;
using Google.GenAI.Types;
using GustosApp.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Environment = System.Environment;

namespace GustosApp.Application.Services
{
    public class RecomendacionAIService : IRecomendacionAIService
    {
        private readonly Client _client;
        private readonly string _model;
        private readonly ILogger<RecomendacionAIService> _logger;

        
        public RecomendacionAIService(
            IOptions<GeminiSettings> opts,
            HttpClient http,
            IConfiguration config,
            ILogger<RecomendacionAIService> logger)
        {
            var cfg = opts?.Value ?? throw new ArgumentNullException(nameof(opts));
            var apiKey = cfg.ApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
                apiKey = config["GeminiSettings:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
                apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("GeminiSettings:ApiKey no configurada. Use user-secrets, appsettings o la variable de entorno GOOGLE_API_KEY.");
            if (string.IsNullOrWhiteSpace(cfg.Model))
                throw new InvalidOperationException("GeminiSettings:Model no está configurado.");

            _model = cfg.Model;
            _logger = logger;
            _client = new Client(apiKey: apiKey);
        }

        public async Task<string> GenerarRecomendacion(string prompt)
        {
            try
            {
                var response = await _client.Models.GenerateContentAsync(
                    model: _model,
                    contents: prompt
                );

                var texto = response.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
                if (string.IsNullOrWhiteSpace(texto))
                    throw new InvalidOperationException("Gemini devolvió una respuesta sin contenido de texto.");
                return texto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló la generación con Gemini usando el modelo {Modelo}", _model);
                throw new InvalidOperationException(
                    "No se pudo obtener una respuesta de Gemini en este momento.", ex);
            }
        }
    }
}

