using System.Net;
using System.Net.Http.Json;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Infraestructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace GustosApp.API.Tests;

public class OpinionRestauranteApiPruebas
{
    [Fact]
    public async Task FlujoHttp_CreaEditaEliminaYPermitePublicarNuevamente()
    {
        var almacenamiento = new Mock<IFileStorageService>();
        almacenamiento
            .Setup(a => a.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.Is<string>(s => s.StartsWith("opiniones/"))))
            .ReturnsAsync((Stream _, string nombre, string? _) => $"https://archivos.test/{nombre}");

        await using var baseFabrica = new FabricaApiGustosAppVotacion();
        await using var fabrica = baseFabrica.WithWebHostBuilder(builder => builder.ConfigureTestServices(servicios =>
        {
            servicios.RemoveAll<IFileStorageService>();
            servicios.AddSingleton(almacenamiento.Object);
        }));
        using var cliente = fabrica.CreateClient();

        Guid restauranteId;
        using (var alcance = fabrica.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<GustosDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.Usuarios.Add(new Usuario("usuario-pruebas-integracion", "usuario@example.test", "Usuario", "Prueba", "usuario", null));
            var restaurante = new Restaurante { Id = Guid.NewGuid(), Nombre = "Restaurante", Direccion = "Calle 123", PlaceId = "place-api-opinion" };
            restauranteId = restaurante.Id;
            db.Restaurantes.Add(restaurante);
            await db.SaveChangesAsync();
        }

        var fechaVisita = DateTime.UtcNow.AddMonths(-2);
        var crear = CrearFormulario(restauranteId, 4.5, "Muy buena experiencia", "Gran visita", fechaVisita, "pareja", "primera.jpg");
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsync("/api/OpinionRestaurante", crear)).StatusCode);

        Guid opinionId;
        Guid fotoId;
        using (var alcance = fabrica.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<GustosDbContext>();
            var opinion = await db.OpinionesRestaurantes.Include(o => o.Fotos).SingleAsync();
            opinionId = opinion.Id;
            fotoId = opinion.Fotos.Single().Id;
            Assert.NotEqual(fechaVisita, opinion.FechaCreacion);
            Assert.Equal(4.5m, (await db.Restaurantes.FindAsync(restauranteId))!.Valoracion);
            Assert.Single(await db.UsuarioRestauranteVisitados.ToListAsync());
        }

        var propia = await cliente.GetAsync($"/api/OpinionRestaurante/mi-opinion/{restauranteId}");
        Assert.Equal(HttpStatusCode.OK, propia.StatusCode);
        var dto = await propia.Content.ReadFromJsonAsync<OpinionPropiaRespuesta>();
        Assert.Equal(opinionId, dto!.Id);
        Assert.Single(dto.Fotos);

        using var editar = new MultipartFormDataContent();
        editar.Add(new StringContent("2.5"), "Valoracion");
        editar.Add(new StringContent("La experiencia cambió"), "Opinion");
        editar.Add(new StringContent("Visita actualizada"), "Titulo");
        editar.Add(new StringContent(DateTime.UtcNow.AddMonths(-1).ToString("O")), "FechaVisita");
        editar.Add(new StringContent("familia"), "MotivoVisita");
        editar.Add(new StringContent(fotoId.ToString()), "FotosConservadasIds");
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.PutAsync($"/api/OpinionRestaurante/{opinionId}", editar)).StatusCode);

        using (var alcance = fabrica.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<GustosDbContext>();
            var opinion = await db.OpinionesRestaurantes.Include(o => o.Fotos).SingleAsync();
            Assert.Equal("La experiencia cambió", opinion.Opinion);
            Assert.Single(opinion.Fotos);
            Assert.Equal(2.5m, (await db.Restaurantes.FindAsync(restauranteId))!.Valoracion);
            Assert.Single(await db.UsuarioRestauranteVisitados.ToListAsync());
        }

        Assert.Equal(HttpStatusCode.NoContent, (await cliente.DeleteAsync($"/api/OpinionRestaurante/{opinionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.GetAsync($"/api/OpinionRestaurante/mi-opinion/{restauranteId}")).StatusCode);

        using (var alcance = fabrica.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<GustosDbContext>();
            Assert.Empty(await db.OpinionesRestaurantes.ToListAsync());
            Assert.Null((await db.Restaurantes.FindAsync(restauranteId))!.Valoracion);
            Assert.Single(await db.UsuarioRestauranteVisitados.ToListAsync());
        }

        var nueva = CrearFormulario(restauranteId, 5, "Una nueva visita excelente", "Volví y mejoró", DateTime.UtcNow.AddDays(-2), "solo", "segunda.png");
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsync("/api/OpinionRestaurante", nueva)).StatusCode);

        using (var alcance = fabrica.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<GustosDbContext>();
            Assert.Single(await db.UsuarioRestauranteVisitados.ToListAsync());
        }
    }

    private static MultipartFormDataContent CrearFormulario(
        Guid restauranteId,
        double valoracion,
        string opinion,
        string titulo,
        DateTime fechaVisita,
        string motivoVisita,
        string nombreArchivo)
    {
        var formulario = new MultipartFormDataContent();
        formulario.Add(new StringContent(restauranteId.ToString()), "RestauranteId");
        formulario.Add(new StringContent(valoracion.ToString(System.Globalization.CultureInfo.InvariantCulture)), "Valoracion");
        formulario.Add(new StringContent(opinion), "Opinion");
        formulario.Add(new StringContent(titulo), "Titulo");
        formulario.Add(new StringContent(fechaVisita.ToString("O")), "FechaVisita");
        formulario.Add(new StringContent(motivoVisita), "MotivoVisita");
        var imagen = new ByteArrayContent(new byte[] { 1, 2, 3, 4 });
        imagen.Headers.ContentType = new("image/jpeg");
        formulario.Add(imagen, "Imagenes", nombreArchivo);
        return formulario;
    }

    private sealed record OpinionPropiaRespuesta(Guid Id, List<FotoRespuesta> Fotos);
    private sealed record FotoRespuesta(Guid Id, string Url);
}
