using System;
using System.Collections.Generic;

namespace GustosApp.API.DTO
{
    public class LimiteGruposAlcanzadoResponse
    {
        public string Mensaje { get; set; } = string.Empty;
        public string TipoPlan { get; set; } = string.Empty;
        public int LimiteActual { get; set; }
        public int GruposActuales { get; set; }
        public BeneficiosPremiumDto BeneficiosPremium { get; set; }
        public string UrlPago { get; set; } = string.Empty;

        public LimiteGruposAlcanzadoResponse(string mensaje, string tipoPlan, int limiteActual, int gruposActuales, BeneficiosPremiumDto beneficios, string urlPago)
        {
            Mensaje = mensaje;
            TipoPlan = tipoPlan;
            LimiteActual = limiteActual;
            GruposActuales = gruposActuales;
            BeneficiosPremium = beneficios;
            UrlPago = urlPago;
        }
    }

    public class BeneficiosPremiumDto
    {
        public List<string> Beneficios { get; set; } = new List<string>();
        public decimal? Precio { get; set; }
        public string Moneda { get; set; } = "ARS";

        public BeneficiosPremiumDto()
        {
            Beneficios = new List<string>
            {
                "Grupos sin límite",
                "Restaurantes favoritos sin límite"
            };
        }
    }

    public class CrearPagoResponse
    {
        public Guid CompraId { get; set; }
        public string? InitPoint { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool Reutilizada { get; set; }
    }

    public class WebhookPagoRequest
    {
        public string Type { get; set; } = string.Empty;
        public WebhookPagoData Data { get; set; } = new WebhookPagoData();
    }

    public class WebhookPagoData
    {
        public string Id { get; set; } = string.Empty;
    }
}
