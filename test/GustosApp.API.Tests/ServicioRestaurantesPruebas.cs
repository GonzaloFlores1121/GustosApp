using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using FluentAssertions;
using GustosApp.Domain.Model;
using GustosApp.Domain.Common;
using GustosApp.Infraestructure.Repositories;
using GustosApp.Infraestructure.Services;
using GustosApp.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text;
using Xunit;

namespace GustosApp.API.Tests.Servicios;

public class ServicioRestaurantesPruebas
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ObtenerResenas_EnFichaConPropietario_ConservaDatosEditables(
        bool propietarioConDuenoId)
    {
        await using var contexto = DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var restauranteId = Guid.NewGuid();
        var restaurante = new Restaurante
        {
            Id = restauranteId,
            PlaceId = "place-reclamado",
            DuenoId = propietarioConDuenoId ? Guid.NewGuid() : null,
            PropietarioUid = propietarioConDuenoId ? string.Empty : "firebase-propietario",
            Nombre = "Nombre del propietario",
            NombreNormalizado = "nombre del propietario",
            Direccion = "Dirección del propietario",
            Latitud = -34.60,
            Longitud = -58.40,
            Categoria = "Categoría elegida",
            ImagenUrl = "https://imagenes.test/propietario.jpg",
            Rating = 3.5,
            CantidadResenas = 10
        };
        contexto.Restaurantes.Add(restaurante);
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();

        var servicio = CrearServicioConRespuestaGoogle(contexto, RespuestaGoogleActualizada());

        await servicio.ObtenerResenasDesdeGooglePlaces("place-reclamado", CancellationToken.None);

        contexto.ChangeTracker.Clear();
        var guardado = await contexto.Restaurantes
            .Include(r => r.Reviews)
            .SingleAsync(r => r.Id == restauranteId);
        guardado.Nombre.Should().Be("Nombre del propietario");
        guardado.NombreNormalizado.Should().Be("nombre del propietario");
        guardado.Direccion.Should().Be("Dirección del propietario");
        guardado.Latitud.Should().Be(-34.60);
        guardado.Longitud.Should().Be(-58.40);
        guardado.Categoria.Should().Be("Categoría elegida");
        guardado.ImagenUrl.Should().Be("https://imagenes.test/propietario.jpg");
        guardado.Rating.Should().Be(4.9);
        guardado.CantidadResenas.Should().Be(250);
        guardado.Reviews.Should().ContainSingle(r =>
            r.EsImportada &&
            r.AutorExterno == "Cliente Google" &&
            r.Opinion == "Excelente");
    }

    [Fact]
    public async Task ObtenerResenas_EnFichaImportada_ActualizaDatosGoogleYPersisteCambios()
    {
        await using var contexto = DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var restauranteId = Guid.NewGuid();
        contexto.Restaurantes.Add(new Restaurante
        {
            Id = restauranteId,
            PlaceId = "place-importado",
            Nombre = "Nombre anterior",
            NombreNormalizado = "nombre anterior",
            Direccion = "Dirección anterior",
            Latitud = -34.60,
            Longitud = -58.40,
            Categoria = "restaurant",
            ImagenUrl = "https://imagenes.test/anterior.jpg"
        });
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();

        var servicio = CrearServicioConRespuestaGoogle(contexto, RespuestaGoogleActualizada());

        await servicio.ObtenerResenasDesdeGooglePlaces("place-importado", CancellationToken.None);

        contexto.ChangeTracker.Clear();
        var guardado = await contexto.Restaurantes.SingleAsync(r => r.Id == restauranteId);
        guardado.Nombre.Should().Be("Nombre desde Google");
        guardado.NombreNormalizado.Should().Be("nombre desde google");
        guardado.Direccion.Should().Be("Dirección desde Google");
        guardado.Latitud.Should().Be(-31.42);
        guardado.Longitud.Should().Be(-64.18);
        guardado.Categoria.Should().Be("italian_restaurant");
        guardado.ImagenUrl.Should().Be("https://imagenes.test/anterior.jpg", "una foto existente sin procedencia suficiente debe conservarse");
        guardado.Rating.Should().Be(4.9);
        guardado.CantidadResenas.Should().Be(250);
        guardado.EstadoUltimaActualizacionExterna.Should().Be(EstadoActualizacionExternaRestaurante.Exitosa);
        guardado.FechaUltimoIntentoActualizacionExternaUtc.Should().NotBeNull();
        guardado.FechaUltimaActualizacionExternaExitosaUtc.Should().NotBeNull();
        guardado.ErrorUltimaActualizacionExterna.Should().BeNull();
    }

    [Fact]
    public async Task ObtenerResenas_CuandoGoogleFalla_ConservaDatosYRegistraErrorInterno()
    {
        await using var contexto = DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var restauranteId = Guid.NewGuid();
        contexto.Restaurantes.Add(new Restaurante
        {
            Id = restauranteId,
            PlaceId = "place-error",
            Nombre = "Ficha conservada",
            NombreNormalizado = "ficha conservada",
            Direccion = "Dirección conservada",
            Rating = 4.2
        });
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();

        var servicio = CrearServicioConRespuestaGoogle(
            contexto,
            "{}",
            HttpStatusCode.ServiceUnavailable);

        var resultado = await servicio.ObtenerResenasDesdeGooglePlaces("place-error", CancellationToken.None);

        resultado.Nombre.Should().Be("Ficha conservada");
        contexto.ChangeTracker.Clear();
        var guardado = await contexto.Restaurantes.SingleAsync(r => r.Id == restauranteId);
        guardado.Nombre.Should().Be("Ficha conservada");
        guardado.Rating.Should().Be(4.2);
        guardado.EstadoUltimaActualizacionExterna.Should().Be(EstadoActualizacionExternaRestaurante.Error);
        guardado.FechaUltimoIntentoActualizacionExternaUtc.Should().NotBeNull();
        guardado.FechaUltimaActualizacionExternaExitosaUtc.Should().BeNull();
        guardado.ErrorUltimaActualizacionExterna.Should().Contain("503");
    }

    [Fact]
    public async Task ConsultaLegacy_ConPropietario_SigueDisponibleYNoDuplicaResenas()
    {
        await using var contexto = DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var ficha = new Restaurante { Id = Guid.NewGuid(), DuenoId = Guid.NewGuid(), PlaceId = "place-reclamado",
            Nombre = "Nombre del propietario", NombreNormalizado = "nombre del propietario", Direccion = "Dirección local", Rating = 3 };
        contexto.Add(ficha);
        await contexto.SaveChangesAsync();
        var servicio = CrearServicioConRespuestaGoogle(contexto, """
            {"reviews":[{"authorAttribution":{"displayName":"Autor"},"text":{"text":"Buena"},"rating":5},
                        {"authorAttribution":{"displayName":"AUTOR"},"text":{"text":"buena"},"rating":5}]}
            """);
        await servicio.ActualizarReviewsDesdeGoogleLegacyAsync(ficha.Id, ficha.PlaceId, default);
        await servicio.ActualizarReviewsDesdeGoogleLegacyAsync(ficha.Id, ficha.PlaceId, default);
        contexto.ChangeTracker.Clear();
        var final = await contexto.Restaurantes.Include(r => r.Reviews).SingleAsync();
        final.Nombre.Should().Be("Nombre del propietario");
        final.Rating.Should().Be(3);
        final.FechaUltimoIntentoActualizacionExternaUtc.Should().BeNull();
        final.Reviews.Should().ContainSingle(r => r.EsImportada && r.FuenteExterna == "GooglePlaces");
    }

    [Fact]
    public async Task ObtenerResenas_EnviaMascaraSinCamposVacios()
    {
        await using var contexto = DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var handler = new MascaraHttpHandler();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GooglePlaces:ApiKey"] = "clave-ficticia" }).Build();
        var servicio = new ServicioRestaurantes(contexto, config, new HttpClient(handler), new RestauranteRepositoryEF(contexto));
        await servicio.ObtenerResenasDesdeGooglePlaces("place-mascara", default);
        handler.Mascara!.Split(',').Should().OnlyContain(campo => !string.IsNullOrWhiteSpace(campo));
    }

    private sealed class MascaraHttpHandler : HttpMessageHandler
    {
        public string? Mascara { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Mascara = request.Headers.GetValues("X-Goog-FieldMask").Single();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") });
        }
    }

    [Theory]
    [InlineData(OrigenImagenRestaurante.Google, false, true)]
    [InlineData(OrigenImagenRestaurante.Google, true, false)]
    [InlineData(OrigenImagenRestaurante.Administracion, false, false)]
    [InlineData(OrigenImagenRestaurante.Propietario, false, false)]
    [InlineData(OrigenImagenRestaurante.Desconocida, false, false)]
    public async Task ActualizacionExterna_SoloRenuevaFotoConOrigenGoogleSinPropietario(OrigenImagenRestaurante origen, bool tieneDueno, bool renueva)
    {
        await using var db = DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var restaurante = new Restaurante { Id = Guid.NewGuid(), PlaceId = "place-importado", Nombre = "Prueba", ImagenUrl = "https://foto.test/original", OrigenImagen = origen, DuenoId = tieneDueno ? Guid.NewGuid() : null };
        db.Add(restaurante); await db.SaveChangesAsync();
        var respuesta = RespuestaGoogleActualizada().Replace("\"id\": \"place-importado\",", "\"id\": \"place-importado\", \"photos\": [{\"name\":\"places/place-importado/photos/actual\"}],");

        await CrearServicioConRespuestaGoogle(db, respuesta).ObtenerResenasDesdeGooglePlaces("place-importado", default);

        db.ChangeTracker.Clear();
        var guardado = await db.Restaurantes.FindAsync(restaurante.Id);
        guardado!.ImagenUrl.Should().Be(renueva ? ImagenRestaurantePublica.GoogleActual : "https://foto.test/original");
        guardado.OrigenImagen.Should().Be(origen);
    }

    private static ServicioRestaurantes CrearServicioConRespuestaGoogle(
        GustosApp.Infraestructure.GustosDbContext contexto,
        string respuesta,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GooglePlaces:ApiKey"] = "api-key-pruebas"
            })
            .Build();
        var http = new HttpClient(new RespuestaHttpHandler(respuesta, statusCode));
        var repositorio = new RestauranteRepositoryEF(contexto);

        return new ServicioRestaurantes(contexto, configuracion, http, repositorio);
    }

    private static string RespuestaGoogleActualizada() => """
        {
          "id": "place-google",
          "displayName": { "text": "Nombre desde Google" },
          "formattedAddress": "Dirección desde Google",
          "location": { "latitude": -31.42, "longitude": -64.18 },
          "primaryType": "italian_restaurant",
          "rating": 4.9,
          "userRatingCount": 250,
          "photos": [{ "name": "places/place-google/photos/foto-google" }],
          "reviews": [{
            "rating": 5,
            "text": { "text": "Excelente" },
            "authorAttribution": {
              "displayName": "Cliente Google",
              "photoUri": "https://imagenes.test/cliente.jpg"
            }
          }]
        }
        """;

    private sealed class RespuestaHttpHandler(
        string respuesta,
        HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(respuesta, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

   /* private static CrearRestauranteDto DtoBasico() => new CrearRestauranteDto
    {
        Nombre = "Parrilla Demo",
        Direccion = "Calle 123",
        Latitud = -34.6,
        Longitud = -58.4,
        Horarios = new { tz = "America/Argentina/Buenos_Aires" },
        Tipo = "Parrilla",
        Platos = new() { "Hamburguesas" },
        ImagenUrl = "https://ejemplo.com/img.jpg",
        Valoracion = 4.2m
    };

        [Fact]
        public async Task Crear_DeberiaCrearYDevolverDto()
        {
            var ctx      = DbContextEnMemoria.Crear(nameof(Crear_DeberiaCrearYDevolverDto));
            var servicio = new ServicioRestaurantes(ctx);

            var dto = DtoBasico();
            var res = await servicio.CrearAsync("uid-1", dto);

        res.Id.Should().NotBeEmpty();
        res.Nombre.Should().Be(dto.Nombre);
        res.Tipo.Should().Be(TipoRestaurante.Parrilla);
        res.Platos.Should().Contain("Hamburguesas");
    }

        [Fact]
        public async Task Crear_CuandoNombreDuplicado_DeberiaFallar()
        {
            var ctx      = DbContextEnMemoria.Crear(nameof(Crear_CuandoNombreDuplicado_DeberiaFallar));
            var servicio = new ServicioRestaurantes(ctx);

            await servicio.CrearAsync("uid-1", DtoBasico());

            var act = async () => await servicio.CrearAsync("uid-2", DtoBasico());
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*ya está en uso*");
        }

        [Fact]
        public async Task Crear_CuandoFaltanCoordenadas_DeberiaFallar()
        {
            var ctx      = DbContextEnMemoria.Crear(nameof(Crear_CuandoFaltanCoordenadas_DeberiaFallar));
            var servicio = new ServicioRestaurantes(ctx);

            var dto = DtoBasico();
            dto.Lat = null; dto.Lng = null;
            dto.Latitud = null; dto.Longitud = null;

            var act = async () => await servicio.CrearAsync("uid-1", dto);
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Lat/Lng*"); // mensaje del servicio
        }

        [Fact]
        public async Task ListarCercanos_FiltraPorPlatos()
        {
            var ctx      = DbContextEnMemoria.Crear(nameof(ListarCercanos_FiltraPorPlatos));
            var servicio = new ServicioRestaurantes(ctx);

            // A: Pasta (cerca)
            var a = DtoBasico();
            a.Nombre = "Trattoria A";
            a.PrimaryType = "italian_restaurant";
            a.Types = new() { "restaurant", "italian_restaurant" };
            a.Platos = new() { "Pasta" };
            a.Lat = -34.600; a.Lng = -58.400;
            await servicio.CrearAsync("u1", a);

            // B: Sushi (cerca)
            var b = DtoBasico();
            b.Nombre = "Sushi B";
            b.PrimaryType = "japanese_restaurant";
            b.Types = new() { "restaurant", "japanese_restaurant" };
            b.Platos = new() { "Sushi" };
            b.Lat = -34.601; b.Lng = -58.401;
            await servicio.CrearAsync("u2", b);

            // C: Postres (lejos)
            var c = DtoBasico();
            c.Nombre = "Trattoria C";
            c.PrimaryType = "italian_restaurant";
            c.Types = new() { "restaurant", "italian_restaurant" };
            c.Platos = new() { "Postres" };
            c.Lat = -35.0; c.Lng = -58.0; // lejos
            await servicio.CrearAsync("u3", c);

            var res = await servicio.ListarCercanosAsync(-34.60, -58.40, 1000, tipo: null, platos: new[] { "Pasta" });

            res.Should().HaveCount(1);
            res.First().Nombre.Should().Be("Trattoria A");
        }

        [Fact]
        public async Task Actualizar_DeberiaReemplazarPlatosYDatos()
        {
            var ctx      = DbContextEnMemoria.Crear(nameof(Actualizar_DeberiaReemplazarPlatosYDatos));
            var servicio = new ServicioRestaurantes(ctx);

            var creado = await servicio.CrearAsync("u1", DtoBasico());

            var dto = new ActualizarRestauranteDto
            {
                Nombre    = "Nuevo Nombre",
                Direccion = "Nueva 456",
                Lat = -34.61, Lng = -58.41,
                Horarios = new { tz = "America/Argentina/Buenos_Aires" },

                PrimaryType = "mexican_restaurant",
                Types       = new() { "restaurant", "mexican_restaurant" },

                Platos     = new() { "Ensaladas" },
                ImagenUrl  = "https://nuevo.jpg",
                Valoracion = 3.9m
            };

            var actualizado = await servicio.ActualizarAsync(creado.Id, "u1", false, dto);

        actualizado.Should().NotBeNull();
        actualizado!.Nombre.Should().Be("Nuevo Nombre");
        actualizado.Tipo.Should().Be("Mexicana");
        actualizado.Platos.Should().BeEquivalentTo(new[] { "Ensaladas" });
    }
   */
}
