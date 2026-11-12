using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GustosApp.API.DTO
{
    public class ActualizarRestauranteDashboardRequest
    {
        
        [JsonPropertyName("direccion")]
        public string? Direccion { get; set; }

        [JsonPropertyName("lat")]
        public double? Latitud { get; set; }

        [JsonPropertyName("lng")]
        public double? Longitud { get; set; }

        [JsonPropertyName("horariosJson")]
        public string? HorariosJson { get; set; }

        private string? _webUrl;

        [JsonPropertyName("webUrl")]
        public string? WebUrl
        {
            get => _webUrl;
            set
            {
                _webUrl = value;
                WebUrlFueEnviado = true;
            }
        }

        [JsonIgnore]
        public bool WebUrlFueEnviado { get; private set; }

        [JsonPropertyName("gustosQueSirveIds")]
        public List<Guid>? GustosQueSirveIds { get; set; }

        [JsonPropertyName("restriccionesQueRespetaIds")]
        public List<Guid>? RestriccionesQueRespetaIds { get; set; }
    }

    public sealed class ConfirmarVigenciaRestauranteRequest
    {
        public int VersionEsperada { get; set; }
    }

    public sealed class PublicarBorradorMenuRequest
    {
        [JsonPropertyName("token")]
        public Guid Token { get; set; }

        [JsonPropertyName("jsonRevisado")]
        public string JsonRevisado { get; set; } = string.Empty;
    }

    public sealed class BorradorMenuRestauranteResponse
    {
        public Guid? Token { get; set; }
        public string Estado { get; set; } = "Ninguno";
        public string? Json { get; set; }
        public string? ImagenFuenteUrl { get; set; }
        public string? ImagenPublicadaUrl { get; set; }
        public string? Error { get; set; }
        public DateTime? FechaUtc { get; set; }
        public bool TieneMenuPublicado { get; set; }
        public int VersionPublicada { get; set; }
    }
}
