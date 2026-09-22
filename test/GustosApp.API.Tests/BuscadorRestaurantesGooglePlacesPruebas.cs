using System.Net;
using System.Text;
using FluentAssertions;
using GustosApp.Application.UseCases.RestauranteUseCases.Importacion;
using GustosApp.Infraestructure.Extrerno.GooglePlacesModel;
using Microsoft.Extensions.Configuration;
using Moq;

namespace GustosApp.API.Tests;

public sealed class BuscadorRestaurantesGooglePlacesPruebas
{
    [Fact]
    public async Task BuscarCercanos_DebeEnviarFiltroGeograficoYMapearDatosBasicos()
    {
        const string respuestaJson = """
            {
              "places": [
                {
                  "id": "place-1",
                  "displayName": { "text": "Café de prueba" },
                  "formattedAddress": "Av. Corrientes 123",
                  "location": { "latitude": -34.6037, "longitude": -58.3816 },
                  "primaryType": "cafe",
                  "types": ["cafe", "food", "establishment"]
                }
              ]
            }
            """;
        var manejador = new ManejadorHttpPrueba(respuestaJson);
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GooglePlaces:ApiKey"] = "clave-prueba"
            })
            .Build();
        var fecha = new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero);
        var reloj = new Mock<TimeProvider>();
        reloj.Setup(provider => provider.GetUtcNow()).Returns(fecha);
        var servicio = new BuscadorRestaurantesGooglePlaces(
            new HttpClient(manejador),
            configuracion,
            reloj.Object);

        var resultado = await servicio.BuscarCercanosAsync(
            new SolicitudDescubrimientoRestaurantes(-34.6037, -58.3816, 1200, 10));

        var restaurante = resultado.Should().ContainSingle().Subject;
        restaurante.PlaceId.Should().Be("place-1");
        restaurante.Nombre.Should().Be("Café de prueba");
        restaurante.PrimaryType.Should().Be("cafe");
        restaurante.TypesJson.Should().Be("[\"cafe\",\"food\",\"establishment\"]");
        restaurante.Rating.Should().BeNull();
        restaurante.FechaDatosUtc.Should().Be(fecha.UtcDateTime);
        manejador.Metodo.Should().Be(HttpMethod.Post);
        manejador.Url.Should().Be("https://places.googleapis.com/v1/places:searchNearby");
        manejador.ClaveApi.Should().Be("clave-prueba");
        manejador.MascaraCampos.Should().NotContain("rating");
        manejador.Cuerpo.Should().Contain("\"radius\":1200");
        manejador.Cuerpo.Should().Contain("\"maxResultCount\":10");
    }

    private sealed class ManejadorHttpPrueba(string respuestaJson) : HttpMessageHandler
    {
        public HttpMethod? Metodo { get; private set; }
        public string? Url { get; private set; }
        public string? ClaveApi { get; private set; }
        public string? MascaraCampos { get; private set; }
        public string? Cuerpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Metodo = request.Method;
            Url = request.RequestUri?.ToString();
            ClaveApi = request.Headers.GetValues("X-Goog-Api-Key").Single();
            MascaraCampos = request.Headers.GetValues("X-Goog-FieldMask").Single();
            Cuerpo = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(respuestaJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
