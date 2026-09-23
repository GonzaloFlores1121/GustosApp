using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases.Importacion;
using Microsoft.Extensions.Configuration;

namespace GustosApp.Infraestructure.Extrerno.GooglePlacesModel;

public sealed class BuscadorRestaurantesGooglePlaces : IBuscadorRestaurantesExternos
{
    private const string UrlBusqueda = "https://places.googleapis.com/v1/places:searchNearby";

    private static readonly string[] TiposIncluidos =
    [
        "bakery",
        "bar",
        "bar_and_grill",
        "cafe",
        "coffee_shop",
        "deli",
        "dessert_shop",
        "food_court",
        "ice_cream_shop",
        "meal_delivery",
        "meal_takeaway",
        "pub",
        "restaurant",
        "sandwich_shop"
    ];

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;

    public BuscadorRestaurantesGooglePlaces(
        HttpClient httpClient,
        IConfiguration configuration,
        TimeProvider timeProvider)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyCollection<RestauranteImportacionEntrada>> BuscarCercanosAsync(
        SolicitudDescubrimientoRestaurantes solicitud,
        CancellationToken ct = default)
    {
        var apiKey = _configuration["GooglePlaces:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("GooglePlaces:ApiKey no está configurada.");

        var cuerpo = new
        {
            includedTypes = TiposIncluidos,
            maxResultCount = solicitud.CantidadMaxima,
            languageCode = "es",
            regionCode = "AR",
            locationRestriction = new
            {
                circle = new
                {
                    center = new
                    {
                        latitude = solicitud.Latitud,
                        longitude = solicitud.Longitud
                    },
                    radius = (double)solicitud.RadioMetros
                }
            }
        };

        using var pedido = new HttpRequestMessage(HttpMethod.Post, UrlBusqueda)
        {
            Content = JsonContent.Create(cuerpo)
        };
        pedido.Headers.TryAddWithoutValidation("X-Goog-Api-Key", apiKey);
        pedido.Headers.TryAddWithoutValidation(
            "X-Goog-FieldMask",
            "places.id,places.displayName,places.formattedAddress,places.location,places.primaryType,places.types");

        using var respuesta = await _httpClient.SendAsync(pedido, ct);

        if (!respuesta.IsSuccessStatusCode)
        {
            var contenidoError = await respuesta.Content.ReadAsStringAsync(ct);

            throw new HttpRequestException(
                $"Google Places respondió {(int)respuesta.StatusCode} " +
                $"{respuesta.StatusCode}. Respuesta: {contenidoError}",
                null,
                respuesta.StatusCode);
        }

        var contenido = await respuesta.Content.ReadFromJsonAsync<RespuestaBusquedaCercana>(cancellationToken: ct);
        var fechaDatosUtc = _timeProvider.GetUtcNow().UtcDateTime;

        return (contenido?.Places ?? [])
            .Where(EsImportable)
            .DistinctBy(place => place.Id, StringComparer.Ordinal)
            .Select(place => Convertir(place, fechaDatosUtc))
            .ToArray();
    }

    private static bool EsImportable(PlaceDetails place) =>
        !string.IsNullOrWhiteSpace(place.Id) &&
        !string.IsNullOrWhiteSpace(place.DisplayName?.Text) &&
        !string.IsNullOrWhiteSpace(place.FormattedAddress) &&
        place.Location is not null;

    private static RestauranteImportacionEntrada Convertir(PlaceDetails place, DateTime fechaDatosUtc)
    {
        var tipos = place.Types?
            .Where(tipo => !string.IsNullOrWhiteSpace(tipo))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        var tipoPrincipal = place.PrimaryType ?? tipos.FirstOrDefault() ?? "restaurant";

        return new RestauranteImportacionEntrada
        {
            PlaceId = place.Id,
            Nombre = place.DisplayName!.Text!.Trim(),
            Direccion = place.FormattedAddress!.Trim(),
            Latitud = place.Location!.Latitude,
            Longitud = place.Location.Longitude,
            HorariosJson = "{}",
            Rating = null,
            CantidadResenas = null,
            Categoria = tipoPrincipal,
            FechaDatosUtc = fechaDatosUtc,
            PrimaryType = tipoPrincipal,
            TypesJson = JsonSerializer.Serialize(tipos),
            ImagenUrl = null
        };
    }

    private sealed class RespuestaBusquedaCercana
    {
        [JsonPropertyName("places")]
        public List<PlaceDetails> Places { get; init; } = [];
    }
}
