using System.Net;
using FluentAssertions;

namespace GustosApp.API.Tests;

public sealed class PruebasBasicasApi : IClassFixture<FabricaApiGustosApp>
{
    private readonly HttpClient _cliente;

    public PruebasBasicasApi(FabricaApiGustosApp fabrica)
    {
        _cliente = fabrica.CreateClient();
    }

    [Fact]
    public async Task ObtenerEndpointProtegido_SinCredenciales_DevuelveNoAutorizado()
    {
        var respuesta = await _cliente.GetAsync("/Gusto");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ObtenerEndpointInexistente_DevuelveNoEncontrado()
    {
        var respuesta = await _cliente.GetAsync("/api/endpoint-inexistente");

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ObtenerPronostico_DevuelveRespuestaJsonExitosa()
    {
        var respuesta = await _cliente.GetAsync("/WeatherForecast");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        respuesta.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }
}
