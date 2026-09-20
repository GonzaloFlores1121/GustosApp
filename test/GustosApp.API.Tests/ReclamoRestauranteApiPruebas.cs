using System.Net;
using System.Net.Http.Json;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using GustosApp.Infraestructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace GustosApp.API.Tests;

public class ReclamoRestauranteApiPruebas
{
    [Fact]
    public async Task ReclamarPorHttp_PersisteVinculoYReutilizaSolicitudPendiente()
    {
        await using var baseFabrica = new FabricaApiGustosAppVotacion();
        await using var fabrica = baseFabrica.WithWebHostBuilder(builder => builder.ConfigureTestServices(servicios =>
        {
            servicios.RemoveAll<IFirebaseAuthService>();
            servicios.AddSingleton(Mock.Of<IFirebaseAuthService>());
            servicios.RemoveAll<IEmailService>();
            servicios.AddSingleton(Mock.Of<IEmailService>());
            servicios.RemoveAll<IFileStorageService>();
            servicios.AddSingleton(Mock.Of<IFileStorageService>());
        }));
        using var cliente = fabrica.CreateClient();
        Guid restauranteId;
        using (var scope = fabrica.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.Usuarios.Add(new Usuario("usuario-pruebas-integracion", "usuario@example.test", "Usuario", "Prueba", "usuario", null)
            { Rol = RolUsuario.Usuario, RegistroInicialCompleto = true });
            var restaurante = new Restaurante { Id = Guid.NewGuid(), Nombre = "Importado", PlaceId = "place-importado" };
            restauranteId = restaurante.Id;
            db.Restaurantes.Add(restaurante);
            await db.SaveChangesAsync();
        }

        var ruta = $"/api/Restaurantes/{restauranteId}/reclamo";
        var respuesta = await cliente.PostAsync(ruta, null);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var solicitudId = await respuesta.Content.ReadFromJsonAsync<Guid>();
        var repetida = await cliente.PostAsync(ruta, null);
        Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        Assert.Equal(solicitudId, await repetida.Content.ReadFromJsonAsync<Guid>());
        using var verificacion = fabrica.Services.CreateScope();
        var contexto = verificacion.ServiceProvider.GetRequiredService<GustosDbContext>();
        var solicitud = await contexto.SolicitudesRestaurantes.SingleAsync();
        Assert.Equal(restauranteId, solicitud.RestauranteExistenteId);
        Assert.Equal(EstadoSolicitudRestaurante.Pendiente, solicitud.Estado);
        Assert.Null((await contexto.Restaurantes.SingleAsync(r => r.Id == restauranteId)).DuenoId);
        Assert.Equal(RolUsuario.PendienteRestaurante, (await contexto.Usuarios.SingleAsync()).Rol);
    }
}
