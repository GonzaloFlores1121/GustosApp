using System.Net;
using FluentAssertions;

namespace GustosApp.API.Tests;

public sealed class ApiSmokeTests : IClassFixture<GustosAppApiFactory>
{
    private readonly HttpClient _client;

    public ApiSmokeTests(GustosAppApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetProtectedEndpoint_WithoutCredentials_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/Gusto");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUnknownEndpoint_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/endpoint-that-does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetWeatherForecast_ReturnsSuccessfulJsonResponse()
    {
        var response = await _client.GetAsync("/WeatherForecast");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }
}
