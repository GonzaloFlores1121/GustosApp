using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Infraestructure;
using GustosApp.Infraestructure.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace GustosApp.API.Tests;

public sealed class FabricaApiGustosAppVotacion : FabricaApiGustosApp
{
    private readonly string _nombreBaseDatos = $"votacion-{Guid.NewGuid()}";

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

            servicios.RemoveAll<GustosDbContext>();
            servicios.RemoveAll<DbContextOptions<GustosDbContext>>();
            servicios.AddDbContext<GustosDbContext>(opciones =>
                opciones.UseInMemoryDatabase(_nombreBaseDatos));

            servicios.RemoveAll<IUsuarioRepository>();
            servicios.AddScoped<IUsuarioRepository, UsuarioRepositoryEF>();

            servicios.RemoveAll<INotificacionesVotacionService>();
            servicios.AddSingleton(Mock.Of<INotificacionesVotacionService>());
        });
    }
}
