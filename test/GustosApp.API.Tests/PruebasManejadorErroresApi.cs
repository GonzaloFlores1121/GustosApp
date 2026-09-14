using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GustosApp.API.Tests;

public sealed class PruebasManejadorErroresApi : IClassFixture<FabricaApiGustosApp>
{
    private readonly HttpClient _cliente;

    public PruebasManejadorErroresApi(FabricaApiGustosApp fabrica)
    {
        _cliente = fabrica.CreateClient();
    }

    [Fact]
    public async Task ErrorNoControlado_NoExponeDetallesInternos()
    {
        var respuesta = await _cliente.GetAsync("/pruebas/errores/interno");
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        var problema = JsonSerializer.Deserialize<ProblemDetails>(cuerpo, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        respuesta.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        problema.Should().NotBeNull();
        problema!.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problema.Title.Should().Be("Error interno del servidor");
        problema.Detail.Should().Be("Ocurrió un error inesperado. Intente nuevamente más tarde.");
        problema.Instance.Should().Be("/pruebas/errores/interno");
        cuerpo.Should().NotContain("detalle-interno-sensible");
        cuerpo.Should().NotContain("causa-interna-sensible");
    }

    [Fact]
    public async Task AccesoARecursoAjeno_DevuelveProhibido()
    {
        var respuesta = await _cliente.GetAsync("/pruebas/errores/prohibido");
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        using var documento = JsonDocument.Parse(cuerpo);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        documento.RootElement.GetProperty("message").GetString()
            .Should().Be("No tenés permisos para acceder a este recurso.");
    }
}
