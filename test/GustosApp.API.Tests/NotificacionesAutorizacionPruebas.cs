using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace GustosApp.API.Tests;

public sealed class NotificacionesAutorizacionPruebas : IClassFixture<FabricaApiGustosApp>
{
    private readonly HttpClient _cliente;

    public NotificacionesAutorizacionPruebas(FabricaApiGustosApp fabrica)
    {
        _cliente = fabrica.CreateClient();
    }

    [Fact]
    public async Task CrearNotificacionParaOtroUsuario_NoExponeEndpointPublico()
    {
        var respuesta = await _cliente.PostAsJsonAsync("/api/Notificacion/solicitud", new
        {
            usuarioDestinoId = Guid.NewGuid(),
            tipoNotificacion = 0,
            nombreUsuario = "atacante"
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("aceptar")]
    [InlineData("rechazar")]
    public async Task ResponderInvitacionGrupo_SinAutenticacion_DevuelveNoAutorizado(string accion)
    {
        var respuesta = await _cliente.PostAsync(
            $"/Grupo/invitaciones/{Guid.NewGuid()}/{accion}",
            null);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
