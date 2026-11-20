using System.Net;
using System.Text;
using GustosApp.Application.UseCases.RestauranteUseCases.Importacion;
using GustosApp.Infraestructure;
using GustosApp.Infraestructure.Repositories;
using GustosApp.Infraestructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GustosApp.API.Tests;

public sealed class DisponibilidadImportacionPruebas
{
    [Fact]
    public async Task ImportarActualizarGoogleYReintentarConservaMismaFichaDesactivada()
    {
        using var fabrica = AdministracionDisponibilidadPruebas.CrearFabrica();
        await AdministracionDisponibilidadPruebas.PrepararAdminAsync(fabrica);
        Guid id;
        string placeId;
        await using (var scope = fabrica.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
            var restaurante = await db.Restaurantes.SingleAsync(r => r.Nombre == "Pizza local");
            id = restaurante.Id;
            placeId = restaurante.PlaceId = "place-baja-conservada";
            restaurante.CambiarDisponibilidad(true, 0, "Conservar baja", DateTime.UtcNow);
            await db.SaveChangesAsync();
        }
        for (var intento = 0; intento < 2; intento++)
        {
            await using var scope = fabrica.Services.CreateAsyncScope();
            var importar = scope.ServiceProvider.GetRequiredService<ImportarRestaurantesUseCase>();
            await importar.HandleAsync([new() { PlaceId = placeId, Nombre = "Nombre externo", Direccion = "Dirección externa",
                Latitud = -34.6, Longitud = -58.4, Rating = 4.2, PrimaryType = "restaurant", TypesJson = "[\"restaurant\"]", HorariosJson = "{}" }], true);
            var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
            using var http = new HttpClient(new RespuestaGoogle());
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GooglePlaces:ApiKey"] = "clave-simulada" }).Build();
            var servicio = new ServicioRestaurantes(db, config, http, new RestauranteRepositoryEF(db));
            await servicio.ActualizarImportadoDesdeGooglePlacesAsync(id, default);
        }
        await using var lectura = fabrica.Services.CreateAsyncScope();
        var final = lectura.ServiceProvider.GetRequiredService<GustosDbContext>();
        var conservado = await final.Restaurantes.SingleAsync(r => r.PlaceId == placeId);
        Assert.Equal(id, conservado.Id);
        Assert.True(conservado.Desactivado);
        Assert.Equal(1, conservado.VersionDisponibilidad);
        Assert.Equal("Conservar baja", conservado.MotivoDisponibilidad);
        Assert.Equal(7, await final.Restaurantes.CountAsync());
        Assert.NotNull(conservado.FechaUltimaActualizacionExternaExitosaUtc);
    }

    [Fact]
    public async Task EdicionCargadaAntesDeBajaNoPuedeSobrescribirSuEstadoConUnGrafoViejo()
    {
        using var fabrica = AdministracionDisponibilidadPruebas.CrearFabrica();
        await AdministracionDisponibilidadPruebas.PrepararAdminAsync(fabrica);
        await using var antiguo = fabrica.Services.CreateAsyncScope();
        var dbAnterior = antiguo.ServiceProvider.GetRequiredService<GustosDbContext>();
        var repo = new RestauranteRepositoryEF(dbAnterior);
        var ficha = await dbAnterior.Restaurantes.FirstAsync();
        await using (var baja = fabrica.Services.CreateAsyncScope())
        {
            await baja.ServiceProvider.GetRequiredService<GustosApp.Application.Interfaces.IAdministracionRestaurantes>()
                .CambiarDisponibilidadAsync(ficha.Id, new(true, 0, "Revisión concurrente"), FabricaFiltrosMapa.Uid, default);
        }
        ficha.Direccion = "Edición de una pestaña anterior";
        await repo.UpdateAsync(ficha, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.SaveChangesAsync());
        await using var lectura = fabrica.Services.CreateAsyncScope();
        var final = lectura.ServiceProvider.GetRequiredService<GustosDbContext>();
        var conservado = await final.Restaurantes.SingleAsync(r => r.Id == ficha.Id);
        Assert.True(conservado.Desactivado);
        Assert.NotEqual(ficha.Direccion, conservado.Direccion);
    }

    private sealed class RespuestaGoogle : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(
                """{"id":"place-baja-conservada","displayName":{"text":"Nombre Google"},"formattedAddress":"Dirección Google","rating":4.3,"userRatingCount":40}""", Encoding.UTF8, "application/json") });
    }
}
