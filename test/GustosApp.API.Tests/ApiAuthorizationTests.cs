using System.Net;
using System.Text;
using FluentAssertions;

namespace GustosApp.API.Tests;

public sealed class ApiAuthorizationTests : IClassFixture<AuthenticatedGustosAppApiFactory>
{
    private readonly HttpClient _client;

    public ApiAuthorizationTests(AuthenticatedGustosAppApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAdminEndpoint_WithoutAdminRole_ReturnsForbidden()
    {
        var response = await _client.GetAsync("/Admin/solicitudes");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutGustos_WithMalformedJson_ReturnsBadRequest()
    {
        using var content = new StringContent("{\"ids\":", Encoding.UTF8, "application/json");

        var response = await _client.PutAsync("/Gusto/gustos", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
