using System.Net;
using System.Text;
using AutoMapper;
using FluentAssertions;
using GustosApp.API.DTO;
using GustosApp.API.Mapping.RestaurantesMapper;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using GustosApp.Infraestructure;
using GustosApp.Infraestructure.Repositories;
using GustosApp.Infraestructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GustosApp.API.Tests;

public sealed class ActualizacionExternaIntegridadPruebas
{
    private static DbContextOptions<GustosDbContext> Opciones() => new DbContextOptionsBuilder<GustosDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
    private static Restaurante Ficha(string place = "place-prueba") => new()
    {
        Id = Guid.NewGuid(), PlaceId = place, Nombre = "Anterior", NombreNormalizado = "anterior",
        Direccion = "Dirección local", ImagenUrl = "https://imagenes.test/manual.jpg", Rating = 3, CantidadResenas = 4
    };
    private static HttpResponseMessage Respuesta(string json = """
        {"displayName":{"text":"Google nuevo"},"formattedAddress":"Dirección Google","rating":4.8,"userRatingCount":100,
         "photos":[{"name":"places/place-prueba/photos/foto"}],
         "reviews":[{"rating":5,"text":{"text":"Buena"},"authorAttribution":{"displayName":"Autor"}},
                    {"rating":5,"text":{"text":"buena"},"authorAttribution":{"displayName":"AUTOR"}}]}
        """, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<CancellationToken, Task<HttpResponseMessage>> respuesta) : HttpMessageHandler
    {
        public int Llamadas { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Llamadas++;
            request.RequestUri!.Query.Should().NotContain("key=");
            request.Headers.Contains("X-Goog-Api-Key").Should().BeTrue();
            return respuesta(ct);
        }
    }
    private static ServicioRestaurantes Servicio(GustosDbContext db, Handler handler, IRestauranteRepository? repo = null) => new(db,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GooglePlaces:ApiKey"] = "clave-ficticia-no-real" }).Build(),
        new HttpClient(handler), repo ?? new RestauranteRepositoryEF(db));

    [Fact]
    public async Task Importado_ActualizaExternos_ConservaMenuRelacionesVigenciasYFotos_YNoDuplicaResenas()
    {
        await using var db = new GustosDbContext(Opciones());
        var ficha = Ficha();
        var gusto = new Gusto { Id = Guid.NewGuid(), Nombre = "Pizza" };
        var restriccion = new Restriccion { Id = Guid.NewGuid(), Nombre = "Sin gluten" };
        ficha.GustosQueSirve.Add(gusto);
        ficha.RestriccionesQueRespeta.Add(restriccion);
        var fecha = DateTime.UtcNow.AddMonths(-2);
        ficha.ConfirmarOfertaVigente(0, fecha);
        ficha.ConfirmarRestriccionesDeclaradas(0, fecha);
        ficha.MenuProcesado = true;
        ficha.Menu = new RestauranteMenu { RestauranteId = ficha.Id, Json = "{\"menu\":\"publicado\"}", Version = 7 };
        var token = Guid.NewGuid();
        ficha.Menu.IniciarBorrador(token, "https://imagenes.test/borrador.png", fecha);
        var imagen = new RestauranteImagen { RestauranteId = ficha.Id, Tipo = TipoImagenRestaurante.Interior, Url = "https://imagenes.test/interior.png" };
        ficha.Imagenes.Add(imagen);
        var local = new OpinionRestaurante { Id = Guid.NewGuid(), RestauranteId = ficha.Id, EsImportada = false, Opinion = "Local", Valoracion = 2 };
        ficha.Reviews.Add(local);
        db.Add(ficha);
        await db.SaveChangesAsync();
        var handler = new Handler(_ => Task.FromResult(Respuesta()));
        var servicio = Servicio(db, handler);
        (await servicio.ActualizarImportadoDesdeGooglePlacesAsync(ficha.Id, default)).Should().Be(ResultadoActualizacionExternaRestaurante.Exitosa);
        (await servicio.ActualizarImportadoDesdeGooglePlacesAsync(ficha.Id, default)).Should().Be(ResultadoActualizacionExternaRestaurante.Exitosa);
        db.ChangeTracker.Clear();
        var final = await db.Restaurantes.Include(r => r.Menu).Include(r => r.Imagenes).Include(r => r.GustosQueSirve)
            .Include(r => r.RestriccionesQueRespeta).Include(r => r.Reviews).SingleAsync();
        final.Nombre.Should().Be("Google nuevo");
        final.Rating.Should().Be(4.8);
        final.ImagenUrl.Should().Be("https://imagenes.test/manual.jpg");
        final.Imagenes.Should().ContainSingle(i => i.Id == imagen.Id && i.Url == imagen.Url);
        final.GustosQueSirve.Should().ContainSingle(g => g.Id == gusto.Id);
        final.RestriccionesQueRespeta.Should().ContainSingle(r => r.Id == restriccion.Id);
        final.FechaRevisionOfertaUtc.Should().Be(fecha);
        final.FechaRevisionRestriccionesUtc.Should().Be(fecha);
        final.VersionOferta.Should().Be(1);
        final.VersionRestricciones.Should().Be(1);
        final.MenuProcesado.Should().BeTrue();
        final.Menu!.Json.Should().Be("{\"menu\":\"publicado\"}");
        final.Menu.Version.Should().Be(7);
        final.Menu.BorradorToken.Should().Be(token);
        final.Reviews.Should().HaveCount(2);
        final.Reviews.Should().ContainSingle(r => r.Id == local.Id && r.Valoracion == 2);
        final.Reviews.Should().ContainSingle(r => r.EsImportada && r.FuenteExterna == "GooglePlaces");
        final.FechaUltimaActualizacionExternaExitosaUtc.Should().NotBeNull();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task ReclamoDuranteConsulta_NoAplicaRespuestaNiErrorSobreNuevoPropietario(bool duenoId, bool fallaGoogle)
    {
        var opciones = Opciones();
        await using var db = new GustosDbContext(opciones);
        var ficha = Ficha();
        db.Add(ficha);
        await db.SaveChangesAsync(); // Se deja seguida para demostrar que también se refresca el scope anterior.
        var handler = new Handler(async ct =>
        {
            await using var reclamo = new GustosDbContext(opciones);
            var actual = await reclamo.Restaurantes.SingleAsync(ct);
            actual.DuenoId = duenoId ? Guid.NewGuid() : null;
            actual.PropietarioUid = duenoId ? "" : "propietario-prueba";
            actual.Nombre = "Elegido al reclamar";
            actual.ImagenUrl = "https://imagenes.test/nueva-manual.jpg";
            await reclamo.SaveChangesAsync(ct);
            return Respuesta(status: fallaGoogle ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
        });
        var resultado = await Servicio(db, handler).ActualizarImportadoDesdeGooglePlacesAsync(ficha.Id, default);
        resultado.Should().Be(ResultadoActualizacionExternaRestaurante.Omitida);
        db.ChangeTracker.Clear();
        var final = await db.Restaurantes.SingleAsync();
        final.Nombre.Should().Be("Elegido al reclamar");
        final.ImagenUrl.Should().Be("https://imagenes.test/nueva-manual.jpg");
        final.Rating.Should().Be(3);
        final.FechaUltimoIntentoActualizacionExternaUtc.Should().BeNull();
        (await db.OpinionesRestaurantes.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PropietarioYaAsignado_NoHaceLlamadaExterna(bool duenoId)
    {
        await using var db = new GustosDbContext(Opciones());
        var ficha = Ficha();
        ficha.DuenoId = duenoId ? Guid.NewGuid() : null;
        ficha.PropietarioUid = duenoId ? "" : "propietario-prueba";
        db.Add(ficha);
        await db.SaveChangesAsync();
        var handler = new Handler(_ => throw new InvalidOperationException("No debe llamarse"));
        (await Servicio(db, handler).ActualizarImportadoDesdeGooglePlacesAsync(ficha.Id, default)).Should().Be(ResultadoActualizacionExternaRestaurante.Omitida);
        handler.Llamadas.Should().Be(0);
    }

    [Fact]
    public async Task FotoManualCargadaDuranteConsulta_SeConservaAlAplicarDatosGoogle()
    {
        var opciones = Opciones();
        await using var db = new GustosDbContext(opciones);
        var ficha = Ficha();
        ficha.ImagenUrl = null;
        db.Add(ficha);
        await db.SaveChangesAsync();
        var handler = new Handler(async ct =>
        {
            await using var edicion = new GustosDbContext(opciones);
            var actual = await edicion.Restaurantes.SingleAsync(ct);
            actual.ImagenUrl = "https://imagenes.test/manual-concurrente.png";
            await edicion.SaveChangesAsync(ct);
            return Respuesta();
        });
        await Servicio(db, handler).ActualizarImportadoDesdeGooglePlacesAsync(ficha.Id, default);
        db.ChangeTracker.Clear();
        (await db.Restaurantes.SingleAsync()).ImagenUrl.Should().Be("https://imagenes.test/manual-concurrente.png");
    }

    [Fact]
    public async Task EliminadoDuranteConsulta_NoLoRecrea()
    {
        var opciones = Opciones();
        await using var db = new GustosDbContext(opciones);
        var ficha = Ficha();
        db.Add(ficha);
        await db.SaveChangesAsync();
        var handler = new Handler(async ct =>
        {
            await using var eliminacion = new GustosDbContext(opciones);
            eliminacion.Remove(await eliminacion.Restaurantes.SingleAsync(ct));
            await eliminacion.SaveChangesAsync(ct);
            return Respuesta();
        });
        (await Servicio(db, handler).ActualizarImportadoDesdeGooglePlacesAsync(ficha.Id, default)).Should().Be(ResultadoActualizacionExternaRestaurante.Omitida);
        (await db.Restaurantes.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("json")]
    [InlineData("transporte")]
    [InlineData("nulo")]
    [InlineData("vacio")]
    public async Task Error_ConservaDatosYUltimoExito_AlmacenaSoloDiagnosticoSeguro(string fallo)
    {
        await using var db = new GustosDbContext(Opciones());
        var ficha = Ficha();
        var exitoAnterior = DateTime.UtcNow.AddDays(-3);
        ficha.RegistrarActualizacionExternaExitosa(exitoAnterior);
        db.Add(ficha);
        await db.SaveChangesAsync();
        var handler = new Handler(_ => fallo == "transporte"
            ? throw new HttpRequestException("SIMULACION_SECRETO_NO_REAL")
            : Task.FromResult(fallo switch
            {
                "json" => Respuesta("SIMULACION_SECRETO_NO_REAL"),
                "nulo" => Respuesta("null"),
                "vacio" => Respuesta("{}"),
                _ => Respuesta("SIMULACION_SECRETO_NO_REAL", HttpStatusCode.ServiceUnavailable)
            }));
        (await Servicio(db, handler).ActualizarImportadoDesdeGooglePlacesAsync(ficha.Id, default)).Should().Be(ResultadoActualizacionExternaRestaurante.Fallida);
        db.ChangeTracker.Clear();
        var final = await db.Restaurantes.SingleAsync();
        final.Nombre.Should().Be("Anterior");
        final.Rating.Should().Be(3);
        final.FechaUltimaActualizacionExternaExitosaUtc.Should().Be(exitoAnterior);
        final.FechaUltimoIntentoActualizacionExternaUtc.Should().BeAfter(exitoAnterior);
        final.EstadoUltimaActualizacionExterna.Should().Be(EstadoActualizacionExternaRestaurante.Error);
        final.ErrorUltimaActualizacionExterna.Should().NotContain("SIMULACION_SECRETO_NO_REAL");
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<RestauranteDetalleProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var publico = System.Text.Json.JsonSerializer.Serialize(mapper.Map<RestauranteDetalleDto>(final));
        publico.Should().NotContain("ErrorUltimaActualizacionExterna").And.NotContain("SIMULACION_SECRETO_NO_REAL");
    }

    [Fact]
    public async Task DatosTextualesVacios_NoBorranCamposValidosAunqueLleguenMetricasNuevas()
    {
        await using var db = new GustosDbContext(Opciones());
        var ficha = Ficha();
        ficha.Categoria = "restaurant";
        db.Add(ficha);
        await db.SaveChangesAsync();
        var handler = new Handler(_ => Task.FromResult(Respuesta("""
            {"displayName":{"text":"  "},"formattedAddress":"","primaryType":"  ","rating":4.8}
            """)));
        await Servicio(db, handler).ActualizarImportadoDesdeGooglePlacesAsync(ficha.Id, default);
        db.ChangeTracker.Clear();
        var final = await db.Restaurantes.SingleAsync();
        final.Nombre.Should().Be("Anterior");
        final.NombreNormalizado.Should().Be("anterior");
        final.Direccion.Should().Be("Dirección local");
        final.Categoria.Should().Be("restaurant");
        final.Rating.Should().Be(4.8);
    }

    [Fact]
    public async Task ConsultaExplicitaRepetida_NoCreaOtraFichaNiDuplicaOpiniones()
    {
        await using var db = new GustosDbContext(Opciones());
        var servicio = Servicio(db, new Handler(_ => Task.FromResult(Respuesta())));
        var primera = await servicio.ObtenerResenasDesdeGooglePlaces("place-prueba", default);
        var segunda = await servicio.ObtenerResenasDesdeGooglePlaces("place-prueba", default);
        primera.Id.Should().NotBeEmpty().And.Be(segunda.Id);
        (await db.Restaurantes.CountAsync()).Should().Be(1);
        (await db.OpinionesRestaurantes.CountAsync()).Should().Be(1);
        segunda.Reviews.Should().ContainSingle();
    }

    [Fact]
    public async Task ResenaSinProcedencia_NoSeReclasificaComoGoogleNiSeUsaParaOcultarUnaNueva()
    {
        await using var db = new GustosDbContext(Opciones());
        var ficha = Ficha();
        var desconocida = new OpinionRestaurante { Id = Guid.NewGuid(), RestauranteId = ficha.Id, AutorExterno = "Autor",
            Opinion = "Buena", Valoracion = 2, EsImportada = true, FuenteExterna = null };
        ficha.Reviews.Add(desconocida);
        db.Add(ficha);
        await db.SaveChangesAsync();
        await Servicio(db, new Handler(_ => Task.FromResult(Respuesta()))).ActualizarImportadoDesdeGooglePlacesAsync(ficha.Id, default);
        db.ChangeTracker.Clear();
        var opiniones = await db.OpinionesRestaurantes.ToListAsync();
        opiniones.Should().HaveCount(2);
        opiniones.Should().ContainSingle(o => o.Id == desconocida.Id && o.FuenteExterna == null && o.Valoracion == 2);
        opiniones.Should().ContainSingle(o => o.FuenteExterna == "GooglePlaces");
    }

    [Fact]
    public async Task FalloDePersistencia_NoSeGuardaAccidentalmenteAlProcesarLaFichaSiguiente()
    {
        await using var db = new GustosDbContext(Opciones());
        var primera = Ficha("primera");
        var segunda = Ficha("segunda");
        db.AddRange(primera, segunda);
        await db.SaveChangesAsync();
        var repo = new Mock<IRestauranteRepository>();
        var guardados = 0;
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(async (CancellationToken ct) =>
        {
            if (++guardados == 1) throw new InvalidOperationException("Fallo controlado");
            await db.SaveChangesAsync(ct);
        });
        var servicio = Servicio(db, new Handler(_ => Task.FromResult(Respuesta())), repo.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.ActualizarImportadoDesdeGooglePlacesAsync(primera.Id, default));
        await servicio.ActualizarImportadoDesdeGooglePlacesAsync(segunda.Id, default);
        db.ChangeTracker.Clear();
        var conservada = await db.Restaurantes.SingleAsync(r => r.Id == primera.Id);
        conservada.Nombre.Should().Be("Anterior");
        conservada.Rating.Should().Be(3);
        conservada.FechaUltimoIntentoActualizacionExternaUtc.Should().BeNull();
        (await db.OpinionesRestaurantes.CountAsync(o => o.RestauranteId == primera.Id)).Should().Be(0);
        (await db.OpinionesRestaurantes.CountAsync(o => o.RestauranteId == segunda.Id)).Should().Be(1);
    }
}
