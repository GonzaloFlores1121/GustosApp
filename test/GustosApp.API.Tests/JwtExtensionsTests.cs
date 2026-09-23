using FluentAssertions;
using GustosApp.API.Extensiones;
using Microsoft.AspNetCore.Http;

namespace GustosApp.API.Tests;

public class JwtExtensionsTests
{
    [Fact]
    public void ObtenerTokenAutenticacion_HeaderFrescoTienePrioridadSobreCookie()
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Headers.Authorization = "Bearer token-header";
        contexto.Request.Headers.Cookie = "token=token-cookie";

        var token = JwtExtensions.ObtenerTokenAutenticacion(contexto.Request);

        token.Should().Be("token-header");
    }

    [Fact]
    public void ObtenerTokenAutenticacion_SignalRUsaQueryAntesQueCookie()
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Path = "/chatHub";
        contexto.Request.QueryString = new QueryString("?access_token=token-signalr");
        contexto.Request.Headers.Cookie = "token=token-cookie";

        var token = JwtExtensions.ObtenerTokenAutenticacion(contexto.Request);

        token.Should().Be("token-signalr");
    }

    [Fact]
    public void ObtenerTokenAutenticacion_NoAceptaTokenQueryFueraDeSignalR()
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Path = "/Usuario/me";
        contexto.Request.QueryString = new QueryString("?access_token=token-query");
        contexto.Request.Headers.Cookie = "token=token-cookie";

        var token = JwtExtensions.ObtenerTokenAutenticacion(contexto.Request);

        token.Should().Be("token-cookie");
    }
}
