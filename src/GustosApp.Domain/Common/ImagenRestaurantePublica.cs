using System.Security.Cryptography;
using System.Text;

namespace GustosApp.Domain.Common;

/// <summary>Referencia interna estable: no almacena nombres/URIs temporales de Places Photos.</summary>
public static class ImagenRestaurantePublica
{
    public const string GoogleActual = "google-places:actual";
    public static bool EsGoogle(string? url) => url == GoogleActual ||
        (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
         (uri.Host.Equals("places.googleapis.com", StringComparison.OrdinalIgnoreCase) ||
          uri.Host.Equals("maps.googleapis.com", StringComparison.OrdinalIgnoreCase)));

    public static string Referencia(string url) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();

    public static string? Url(Guid restauranteId, string? url) => !EsGoogle(url) ? url :
        restauranteId == Guid.Empty ? null : $"/api/restaurante/{restauranteId:D}/foto-google/{Referencia(url!)}";
}
