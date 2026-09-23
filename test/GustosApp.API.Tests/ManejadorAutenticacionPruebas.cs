using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GustosApp.API.Tests;

internal sealed class ManejadorAutenticacionPruebas : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string NombreEsquema = "PruebasIntegracion";

    public ManejadorAutenticacionPruebas(
        IOptionsMonitor<AuthenticationSchemeOptions> opciones,
        ILoggerFactory fabricaRegistro,
        UrlEncoder codificador)
        : base(opciones, fabricaRegistro, codificador)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("user_id", "usuario-pruebas-integracion"),
            new Claim("rol", "Usuario")
        };
        var identidad = new ClaimsIdentity(claims, NombreEsquema);
        var usuario = new ClaimsPrincipal(identidad);
        var ticket = new AuthenticationTicket(usuario, NombreEsquema);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
