using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Common;
using GustosApp.Domain.Model.@enum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;

namespace GustosApp.Infraestructure.Services;

public sealed class FotoGoogleService(GustosDbContext db, HttpClient http, IConfiguration config,
    IOptions<FotosGoogleOptions> options, PresupuestoFotosGoogle presupuesto, ILogger<FotoGoogleService> logger) : IFotoGoogleService
{
    public async Task<FotoGoogleResultado?> ObtenerAsync(Guid restauranteId, string referencia, CancellationToken ct)
    {
        if (!options.Value.Habilitadas || !Regex.IsMatch(referencia, "^[a-f0-9]{64}$") || !presupuesto.Entrar()) return null;
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSegundos));
        try
        {
            var restaurante = await db.Restaurantes.AsNoTracking().Include(r => r.Imagenes).Include(r => r.Menu)
                .SingleOrDefaultAsync(r => r.Id == restauranteId, limite.Token);
            if (restaurante == null || !Regex.IsMatch(restaurante.PlaceId ?? "", "^[A-Za-z0-9_-]{1,300}$")) return null;
            var candidatos = new List<(string? Url, OrigenImagenRestaurante Origen)>
                { (restaurante.ImagenUrl, restaurante.OrigenImagen), (restaurante.LogoUrl, restaurante.OrigenLogo) };
            candidatos.AddRange(restaurante.Imagenes.Where(i => i.Tipo != TipoImagenRestaurante.Menu || restaurante.MenuProcesado == true).Select(i => ((string?)i.Url, i.Origen)));
            if (restaurante.MenuProcesado == true && restaurante.Menu != null)
                candidatos.Add((restaurante.Menu.ImagenFuentePublicadaUrl, restaurante.Menu.OrigenImagenPublicada));
            var seleccion = candidatos.FirstOrDefault(i => ImagenRestaurantePublica.EsGoogle(i.Url) && ImagenRestaurantePublica.Referencia(i.Url!) == referencia);
            if (seleccion.Url == null || (seleccion.Url == ImagenRestaurantePublica.GoogleActual && seleccion.Origen != OrigenImagenRestaurante.Google)) return null;
            var key = config["GooglePlaces:ApiKey"];
            if (string.IsNullOrWhiteSpace(key)) return null;

            // Se pide un nombre fresco cada vez. No se persisten nombre, photoUri, autores ni bytes de Google.
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://places.googleapis.com/v1/places/{Uri.EscapeDataString(restaurante.PlaceId!)}?languageCode=es");
            request.Headers.Add("X-Goog-Api-Key", key);
            request.Headers.Add("X-Goog-FieldMask", "photos");
            using var details = await LeerJsonAsync(request, limite.Token);
            if (details == null || !details.RootElement.TryGetProperty("photos", out var photos) || photos.ValueKind != JsonValueKind.Array) return null;
            string? nombreHistorico = null;
            if (seleccion.Origen != OrigenImagenRestaurante.Google && seleccion.Url != ImagenRestaurantePublica.GoogleActual)
            {
                if (!Uri.TryCreate(seleccion.Url, UriKind.Absolute, out var vieja) || vieja.Scheme != "https" ||
                    vieja.Host != "places.googleapis.com" || vieja.UserInfo.Length > 0 || !vieja.IsDefaultPort) return null;
                nombreHistorico = Uri.UnescapeDataString(vieja.AbsolutePath).TrimStart('/');
                if (!nombreHistorico.StartsWith("v1/", StringComparison.Ordinal) || !nombreHistorico.EndsWith("/media", StringComparison.Ordinal)) return null;
                nombreHistorico = nombreHistorico[3..^6];
            }
            JsonElement? foto = null;
            foreach (var candidate in photos.EnumerateArray())
            {
                var name = Texto(candidate, "name");
                if (name != null && !name.Contains(key, StringComparison.Ordinal) && NombreValido(name, restaurante.PlaceId!) && (nombreHistorico == null || name == nombreHistorico)) { foto = candidate; break; }
            }
            if (foto == null) return null; // Nunca sustituir silenciosamente una foto histórica desconocida.
            var nombre = Texto(foto.Value, "name")!;
            using var mediaRequest = new HttpRequestMessage(HttpMethod.Get, $"https://places.googleapis.com/v1/{nombre}/media?maxWidthPx=800&skipHttpRedirect=true");
            mediaRequest.Headers.Add("X-Goog-Api-Key", key);
            using var media = await LeerJsonAsync(mediaRequest, limite.Token);
            var photoUri = media == null ? null : Texto(media.RootElement, "photoUri");
            if (!UriSegura(photoUri, key, cdn: true, out var imagen)) return null;
            using var imageRequest = new HttpRequestMessage(HttpMethod.Get, imagen);
            // No se reutilizan cabeceras autenticadas al consultar el CDN.
            using var imageResponse = await http.SendAsync(imageRequest, HttpCompletionOption.ResponseHeadersRead, limite.Token);
            if (imageResponse.StatusCode != HttpStatusCode.OK) return null; // Sin redirecciones.
            var mime = imageResponse.Content.Headers.ContentType?.MediaType;
            if (mime is not ("image/png" or "image/jpeg" or "image/webp" or "image/gif")) return null;
            var bytes = await LeerAcotadoAsync(imageResponse, options.Value.MaximoBytes, limite.Token);
            if (bytes == null || bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(key)) >= 0) return null;
            var info = Image.Identify(bytes);
            if (info == null || info.Width > 4800 || info.Height > 4800 || (long)info.Width * info.Height > 24_000_000) return null;
            var formato = info.Metadata.DecodedImageFormat?.DefaultMimeType;
            if (formato != mime) return null;
            var autores = new List<AutorFotoGoogle>();
            if (foto.Value.TryGetProperty("authorAttributions", out var attrs) && attrs.ValueKind == JsonValueKind.Array)
                foreach (var autor in attrs.EnumerateArray().Take(20))
                {
                    var nombreAutor = Texto(autor, "displayName");
                    if (string.IsNullOrWhiteSpace(nombreAutor) || nombreAutor.Length > 200 || nombreAutor.Contains(key, StringComparison.Ordinal)) return null;
                    autores.Add(new(nombreAutor, Enlace(autor, "uri", key, false), Enlace(autor, "photoUri", key, true)));
                }
            return new(Convert.ToBase64String(bytes), mime!, autores, Enlace(foto.Value, "googleMapsUri", key, false));
        }
        catch (Exception ex)
        {
            logger.LogWarning("Foto Google no disponible para {RestauranteId}. Tipo: {Tipo}", restauranteId, ex.GetType().Name);
            return null;
        }
        finally { presupuesto.Salir(); }
    }

    private static bool NombreValido(string name, string placeId) =>
        name.StartsWith($"places/{placeId}/photos/", StringComparison.Ordinal) &&
        Regex.IsMatch(name, "^places/[A-Za-z0-9_-]{1,300}/photos/[A-Za-z0-9_-]{1,4096}$");
    private static string? Texto(JsonElement value, string field) => value.TryGetProperty(field, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    private static string? Enlace(JsonElement value, string field, string key, bool cdn) =>
        UriSegura(Texto(value, field), key, cdn, out var uri) ? uri!.AbsoluteUri : null;
    private static bool UriSegura(string? text, string key, bool cdn, out Uri? uri)
    {
        uri = null;
        if (text?.StartsWith("//", StringComparison.Ordinal) == true) text = "https:" + text;
        return text != null && !text.Contains(key, StringComparison.Ordinal) && Uri.TryCreate(text, UriKind.Absolute, out uri) &&
            uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
            (cdn ? Regex.IsMatch(uri.Host, "^lh[3-6]\\.googleusercontent\\.com$") :
                (uri.Host is "www.google.com" or "maps.google.com") && uri.AbsolutePath.StartsWith("/maps", StringComparison.Ordinal));
    }
    private async Task<JsonDocument?> LeerJsonAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode != HttpStatusCode.OK) return null;
        var bytes = await LeerAcotadoAsync(response, 131_072, ct);
        return bytes == null ? null : JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
    }
    private static async Task<byte[]?> LeerAcotadoAsync(HttpResponseMessage response, int maximo, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength > maximo) return null;
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var bloque = new byte[8192];
        int n;
        while ((n = await stream.ReadAsync(bloque.AsMemory(), ct)) > 0)
        {
            if (buffer.Length + n > maximo) return null;
            buffer.Write(bloque, 0, n);
        }
        return buffer.ToArray();
    }
}
