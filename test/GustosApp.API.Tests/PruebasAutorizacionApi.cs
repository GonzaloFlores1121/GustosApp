using System.Net;
using System.Text;
using FluentAssertions;

namespace GustosApp.API.Tests;

public sealed class PruebasAutorizacionApi : IClassFixture<FabricaApiGustosAppAutenticada>
{
    private readonly HttpClient _cliente;

    public PruebasAutorizacionApi(FabricaApiGustosAppAutenticada fabrica)
    {
        _cliente = fabrica.CreateClient();
    }

    [Fact]
    public async Task ObtenerEndpointAdministrador_SinRolAdministrador_DevuelveProhibido()
    {
        var respuesta = await _cliente.GetAsync("/Admin/solicitudes");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ActualizarGustos_ConJsonMalformado_DevuelveSolicitudInvalida()
    {
        using var contenido = new StringContent("{\"ids\":", Encoding.UTF8, "application/json");

        var respuesta = await _cliente.PutAsync("/Gusto/gustos", contenido);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("/Admin/solicitudes/aprobar/11111111-1111-1111-1111-111111111111")]
    [InlineData("/Admin/solicitudes/11111111-1111-1111-1111-111111111111/reprocesar-menu")]
    [InlineData("/Admin/restaurantes/descubrir-cercanos")]
    public async Task GestionarSolicitud_SinRolAdministrador_DevuelveProhibido(string ruta)
    {
        var respuesta = await _cliente.PostAsync(ruta, null);
        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
