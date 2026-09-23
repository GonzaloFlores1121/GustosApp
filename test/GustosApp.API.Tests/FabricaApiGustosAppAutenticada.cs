using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace GustosApp.API.Tests;

public sealed class FabricaApiGustosAppAutenticada : FabricaApiGustosApp
{
    protected override void ConfigureWebHost(IWebHostBuilder constructor)
    {
        base.ConfigureWebHost(constructor);
        constructor.ConfigureTestServices(servicios =>
        {
            servicios
                .AddAuthentication(opciones =>
                {
                    opciones.DefaultAuthenticateScheme = ManejadorAutenticacionPruebas.NombreEsquema;
                    opciones.DefaultChallengeScheme = ManejadorAutenticacionPruebas.NombreEsquema;
                    opciones.DefaultForbidScheme = ManejadorAutenticacionPruebas.NombreEsquema;
                })
                .AddScheme<AuthenticationSchemeOptions, ManejadorAutenticacionPruebas>(
                    ManejadorAutenticacionPruebas.NombreEsquema,
                    _ => { });
        });
    }
}
