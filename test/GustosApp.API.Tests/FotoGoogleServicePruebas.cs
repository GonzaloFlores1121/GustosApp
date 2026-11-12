using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using GustosApp.API.Middleware;
using GustosApp.API.Tests.Infraestructura;
using GustosApp.Domain.Common;
using GustosApp.Domain.Model;
using GustosApp.Infraestructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GustosApp.API.Tests;

public sealed class FotoGoogleServicePruebas
{
    private const string Clave = "clave-ficticia-solo-pruebas-123456";
    private const string Nombre = "places/place-prueba/photos/foto-actual";
    private const string Cdn = "https://lh3.googleusercontent.com/places/imagen";

    [Fact]
    public async Task FotoActual_ConsultaNombreFrescoYEntregaPixelesConCreditosSinClave()
    {
        await using var db = DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var restaurante = CrearRestaurante();
        db.Add(restaurante);
        await db.SaveChangesAsync();
        var handler = new ProveedorSimulado();
        var servicio = CrearServicio(db, handler);

        var resultado = await servicio.ObtenerAsync(restaurante.Id, ImagenRestaurantePublica.Referencia(restaurante.ImagenUrl!), default);

        resultado.Should().NotBeNull();
        resultado!.TipoContenido.Should().Be("image/png");
        Convert.FromBase64String(resultado.ContenidoBase64).Should().Equal(handler.Pixeles);
        resultado.Autores.Should().ContainSingle(a => a.Nombre == "Autora de prueba" && a.PerfilUrl == "https://maps.google.com/maps/contrib/123");
        resultado.FuenteUrl.Should().Be("https://www.google.com/maps/place/prueba");
        handler.Solicitudes.Should().HaveCount(3);
        handler.Solicitudes.Take(2).Should().OnlyContain(r => r.Clave == Clave && !r.Url.Contains(Clave));
        handler.Solicitudes[2].Clave.Should().BeNull();
        handler.Solicitudes[2].Url.Should().Be(Cdn);
        JsonSerializer.Serialize(resultado).Should().NotContain(Clave).And.NotContain(Nombre).And.NotContain(Cdn);
        db.ChangeTracker.Clear();
        (await db.Restaurantes.FindAsync(restaurante.Id))!.ImagenUrl.Should().Be(ImagenRestaurantePublica.GoogleActual);
    }

    [Theory]
    [InlineData("http://127.0.0.1/imagen")]
    [InlineData("https://evil.test/imagen")]
    [InlineData("https://lh3.googleusercontent.com.evil.test/imagen")]
    [InlineData("https://usuario@lh3.googleusercontent.com/imagen")]
    [InlineData("https://lh3.googleusercontent.com:444/imagen")]
    [InlineData("https://lh3.googleusercontent.com/imagen?key=" + Clave)]
    public async Task UriNoPermitida_NoSeConsultaNiSeExpone(string uri)
    {
        await using var db = DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var restaurante = CrearRestaurante();
        db.Add(restaurante); await db.SaveChangesAsync();
        var handler = new ProveedorSimulado { UriImagen = uri };
        (await CrearServicio(db, handler).ObtenerAsync(restaurante.Id, ImagenRestaurantePublica.Referencia(restaurante.ImagenUrl!), default)).Should().BeNull();
        handler.Solicitudes.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task RedireccionEnCualquierPaso_NoSeSigue(int paso)
    {
        await using var db = DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var restaurante = CrearRestaurante();
        db.Add(restaurante); await db.SaveChangesAsync();
        var handler = new ProveedorSimulado { RedireccionEnPaso = paso };
        (await CrearServicio(db, handler).ObtenerAsync(restaurante.Id, ImagenRestaurantePublica.Referencia(restaurante.ImagenUrl!), default)).Should().BeNull();
        handler.Solicitudes.Should().HaveCount(paso);
    }

    [Theory]
    [InlineData("foto-actual", true)]
    [InlineData("foto-vencida", false)]
    public async Task HistoricaDesconocida_ExigeLaMismaFotoEnRespuestaFresca(string foto, bool disponible)
    {
        await using var db = DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var restaurante = CrearRestaurante();
        restaurante.OrigenImagen = OrigenImagenRestaurante.Desconocida;
        restaurante.ImagenUrl = $"https://places.googleapis.com/v1/places/place-prueba/photos/{foto}/media?key={Clave}";
        db.Add(restaurante); await db.SaveChangesAsync();
        var handler = new ProveedorSimulado();
        var resultado = await CrearServicio(db, handler).ObtenerAsync(restaurante.Id, ImagenRestaurantePublica.Referencia(restaurante.ImagenUrl), default);
        (resultado != null).Should().Be(disponible);
        handler.Solicitudes.Should().HaveCount(disponible ? 3 : 1);
        db.ChangeTracker.Clear();
        (await db.Restaurantes.FindAsync(restaurante.Id))!.OrigenImagen.Should().Be(OrigenImagenRestaurante.Desconocida);
    }

    [Theory]
    [InlineData("sin-fotos")]
    [InlineData("json-invalido")]
    [InlineData("error")]
    [InlineData("tipo-invalido")]
    [InlineData("bytes-invalidos")]
    [InlineData("exceso")]
    [InlineData("cancelacion")]
    public async Task FallosDelProveedor_DevuelvenAusenciaSinContenidoTecnico(string fallo)
    {
        await using var db = DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var restaurante = CrearRestaurante();
        db.Add(restaurante); await db.SaveChangesAsync();
        var handler = new ProveedorSimulado { Fallo = fallo };
        (await CrearServicio(db, handler).ObtenerAsync(restaurante.Id, ImagenRestaurantePublica.Referencia(restaurante.ImagenUrl!), default)).Should().BeNull();
    }

    [Fact]
    public async Task DeshabilitadoReferenciaAjenaOMarcadorSinOrigen_NoLlamaProveedor()
    {
        await using var db = DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var restaurante = CrearRestaurante();
        restaurante.OrigenImagen = OrigenImagenRestaurante.Desconocida;
        db.Add(restaurante); await db.SaveChangesAsync();
        var handler = new ProveedorSimulado();
        var servicio = CrearServicio(db, handler);
        (await servicio.ObtenerAsync(restaurante.Id, "url-arbitraria", default)).Should().BeNull();
        (await servicio.ObtenerAsync(Guid.NewGuid(), new string('a', 64), default)).Should().BeNull();
        (await servicio.ObtenerAsync(restaurante.Id, ImagenRestaurantePublica.Referencia(restaurante.ImagenUrl!), default)).Should().BeNull();
        (await CrearServicio(db, handler, false).ObtenerAsync(restaurante.Id, new string('a', 64), default)).Should().BeNull();
        handler.Solicitudes.Should().BeEmpty();
    }

    [Fact]
    public void FronteraPublica_NormalizaGoogleSinRomperTokenDeFirebase()
    {
        var id = Guid.NewGuid();
        var antigua = $"https://places.googleapis.com/v1/{Nombre}/media?key={Clave}";
        const string firebase = "https://firebasestorage.googleapis.com/v0/b/demo/o/foto?alt=media&token=token-publico-ficticio";
        var node = JsonSerializer.SerializeToNode(new { id, nombre = "Prueba", imagenUrl = antigua, logoUrl = firebase, imagenes = new[] { antigua } });
        var resultado = FotosGooglePublicasFilter.Normalizar(node, Guid.Empty)!.ToJsonString();
        resultado.Should().Contain($"/api/restaurante/{id}/foto-google/").And.NotContain(Clave).And.Contain("token-publico-ficticio");
        ImagenRestaurantePublica.Url(Guid.Empty, antigua).Should().BeNull();
        ImagenRestaurantePublica.Url(id, firebase).Should().Be(firebase);
        new Restaurante().OrigenImagen.Should().Be(OrigenImagenRestaurante.Desconocida);
        new RestauranteImagen().Origen.Should().Be(OrigenImagenRestaurante.Desconocida);
    }

    [Fact]
    public void Presupuesto_LimitaConcurrenciaYConsumoPorProceso()
    {
        var presupuesto = new PresupuestoFotosGoogle();
        for (var i = 0; i < 4; i++) presupuesto.Entrar().Should().BeTrue();
        presupuesto.Entrar().Should().BeFalse();
        for (var i = 0; i < 4; i++) presupuesto.Salir();
        for (var i = 4; i < 60; i++) { presupuesto.Entrar().Should().BeTrue(); presupuesto.Salir(); }
        presupuesto.Entrar().Should().BeFalse();
    }

    [Fact]
    public async Task ErrorInterno_NoRegistraMensajeExcepcionNiClave()
    {
        await using var db = DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var restaurante = CrearRestaurante(); db.Add(restaurante); await db.SaveChangesAsync();
        var registro = new RegistroPrueba();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GooglePlaces:ApiKey"] = Clave }).Build();
        var servicio = new FotoGoogleService(db, new HttpClient(new ProveedorSimulado { Fallo = "error" }), config,
            Options.Create(new FotosGoogleOptions { Habilitadas = true }), new PresupuestoFotosGoogle(), registro);

        (await servicio.ObtenerAsync(restaurante.Id, ImagenRestaurantePublica.Referencia(restaurante.ImagenUrl!), default)).Should().BeNull();

        registro.Mensajes.Should().ContainSingle().Which.Should().Contain("HttpRequestException").And.NotContain(Clave).And.NotContain("No publicar");
    }

    private sealed class RegistroPrueba : ILogger<FotoGoogleService>
    {
        public List<string> Mensajes { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { Mensajes.Add(formatter(state, exception)); exception.Should().BeNull(); }
    }

    private static Restaurante CrearRestaurante() => new() { Id = Guid.NewGuid(), Nombre = "Prueba", PlaceId = "place-prueba", ImagenUrl = ImagenRestaurantePublica.GoogleActual, OrigenImagen = OrigenImagenRestaurante.Google };
    private static FotoGoogleService CrearServicio(GustosApp.Infraestructure.GustosDbContext db, ProveedorSimulado handler, bool habilitado = true) => new(db, new HttpClient(handler), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GooglePlaces:ApiKey"] = Clave }).Build(), Options.Create(new FotosGoogleOptions { Habilitadas = habilitado, MaximoBytes = 1024 }), new PresupuestoFotosGoogle(), NullLogger<FotoGoogleService>.Instance);

    private sealed class ProveedorSimulado : HttpMessageHandler
    {
        public List<(string Url, string? Clave)> Solicitudes { get; } = [];
        public string UriImagen { get; init; } = Cdn;
        public string? Fallo { get; init; }
        public int RedireccionEnPaso { get; init; }
        public byte[] Pixeles { get; }
        public ProveedorSimulado()
        {
            using var image = new Image<Rgba32>(2, 2);
            using var buffer = new MemoryStream(); image.SaveAsPng(buffer); Pixeles = buffer.ToArray();
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Solicitudes.Add((request.RequestUri!.AbsoluteUri, request.Headers.TryGetValues("X-Goog-Api-Key", out var keys) ? keys.Single() : null));
            var paso = Solicitudes.Count;
            if (Fallo == "error") throw new HttpRequestException("No publicar " + Clave);
            if (Fallo == "cancelacion") throw new OperationCanceledException(Clave);
            if (RedireccionEnPaso == paso) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://evil.test/?key=" + Clave) } });
            HttpContent content = paso switch
            {
                1 when Fallo == "json-invalido" => new StringContent("{" + Clave),
                1 when Fallo == "sin-fotos" => JsonContent.Create(new { photos = Array.Empty<object>() }),
                1 => JsonContent.Create(new { photos = new[] { new { name = Nombre, googleMapsUri = "https://www.google.com/maps/place/prueba", authorAttributions = new[] { new { displayName = "Autora de prueba", uri = "//maps.google.com/maps/contrib/123", photoUri = "//lh3.googleusercontent.com/a-/avatar" } } } } }),
                2 => JsonContent.Create(new { photoUri = UriImagen }),
                _ => new ByteArrayContent(Fallo == "exceso" ? new byte[1025] : Fallo == "bytes-invalidos" ? new byte[3] : Pixeles)
            };
            if (paso == 3) content.Headers.ContentType = new(Fallo == "tipo-invalido" ? "text/html" : "image/png");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
