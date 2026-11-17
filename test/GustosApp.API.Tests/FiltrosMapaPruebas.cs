using System.Net;
using System.Text.Json;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Infraestructure;
using GustosApp.Infraestructure.ML;
using GustosApp.Infraestructure.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GustosApp.API.Tests;

public sealed class FiltrosMapaPruebas
{
    [Fact]
    public async Task BuscaConCriteriosCombinadosYConElModeloReal()
    {
        using var fabrica = new FabricaFiltrosMapa();
        await fabrica.PrepararAsync();
        using var cliente = fabrica.CreateClient();

        await Verificar(cliente, "", "Pizza local", "Pasta Google", "Tacos sin reseñas", "Pizza local baja", "Pizza al borde");
        var solo = await Verificar(cliente, "&gustos=Pasta", "Pasta Google");
        var conAmiga = await Verificar(cliente, "&gustos=Pasta&amigoUsername=amigafiltros", "Pasta Google");
        Assert.NotEqual(solo["Pasta Google"], conAmiga["Pasta Google"]);
        await Verificar(cliente, "&gustos=Pasta&gustos=Tacos", "Pasta Google", "Tacos sin reseñas");
        await Verificar(cliente, "&gustos=Pizza&gustos=Pasta&rating=4&amigoUsername=amigafiltros",
            "Pizza local", "Pasta Google", "Pizza al borde");
        await Verificar(cliente, "&gustos=Tacos&rating=4");
        await Verificar(cliente, "&near.lat=0&near.lng=0&radiusMeters=3000", "Pizza local", "Pasta Google", "Tacos sin reseñas", "Pizza local baja", "Pizza al borde");
        await Verificar(cliente, "&near.lat=0&near.lng=0&radiusMeters=6000",
            "Pizza local", "Pasta Google", "Tacos sin reseñas", "Pizza local baja", "Pizza al borde", "Pasta fuera del radio", "Pizza esquina fuera");
        await Verificar(cliente, "&near.lat=0.052&near.lng=0&radiusMeters=1000", "Pasta fuera del radio");
    }

    [Fact]
    public async Task DistingueAmigoInexistenteNoAutorizadoYPerfilSinPreferencias()
    {
        using var fabrica = new FabricaFiltrosMapa();
        await fabrica.PrepararAsync();
        using var cliente = fabrica.CreateClient();
        const string ruta = "/api/Restaurantes?near.lat=0&near.lng=0";
        using var inexistente = await cliente.GetAsync(ruta + "&amigoUsername=inexistente");
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        using var pendiente = await cliente.GetAsync(ruta + "&amigoUsername=pendientefiltros");
        Assert.Equal(HttpStatusCode.Forbidden, pendiente.StatusCode);
        await fabrica.RevocarAmistadAsync();
        using var revocada = await cliente.GetAsync(ruta + "&amigoUsername=amigafiltros");
        Assert.Equal(HttpStatusCode.Forbidden, revocada.StatusCode);
        await fabrica.QuitarPreferenciasAsync();
        using var sinPreferencias = await cliente.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sinPreferencias.StatusCode);
        await Verificar(cliente, "&gustos=Pasta", "Pasta Google");
    }

    private static async Task<Dictionary<string, double>> Verificar(HttpClient cliente, string filtros, params string[] nombres)
    {
        var zona = filtros.Contains("near.lat") ? "" : "&near.lat=0&near.lng=0&radiusMeters=3000";
        using var respuesta = await cliente.GetAsync("/api/Restaurantes?top=200" + zona + filtros);
        respuesta.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var resultados = json.RootElement.GetProperty("recomendaciones").EnumerateArray().ToArray();
        Assert.Equal(nombres.Order(), resultados.Select(r => r.GetProperty("nombre").GetString()).Order());
        Assert.Equal(nombres.Length == 0 ? "SinCoincidencias" : "Encontrados", json.RootElement.GetProperty("estado").GetString());
        if (resultados.FirstOrDefault(r => r.GetProperty("nombre").GetString() == "Pizza local") is { ValueKind: JsonValueKind.Object } pizza)
            Assert.Equal("GustosApp", pizza.GetProperty("fuentePuntuacionPrincipal").GetString());
        if (resultados.FirstOrDefault(r => r.GetProperty("nombre").GetString() == "Pasta Google") is { ValueKind: JsonValueKind.Object } pasta)
            Assert.Equal("Google", pasta.GetProperty("fuentePuntuacionPrincipal").GetString());
        return resultados.ToDictionary(r => r.GetProperty("nombre").GetString()!, r => r.GetProperty("score").GetDouble());
    }
}

public sealed class FabricaFiltrosMapa : FabricaApiGustosApp
{
    private readonly string _base = $"filtros-mapa-{Guid.NewGuid():N}";
    public const string Uid = "usuario-pruebas-integracion";

    protected override void ConfigureWebHost(IWebHostBuilder constructor)
    {
        base.ConfigureWebHost(constructor);
        constructor.ConfigureTestServices(servicios =>
        {
            servicios.AddAuthentication(opciones =>
            {
                opciones.DefaultAuthenticateScheme = ManejadorAutenticacionPruebas.NombreEsquema;
                opciones.DefaultChallengeScheme = ManejadorAutenticacionPruebas.NombreEsquema;
                opciones.DefaultForbidScheme = ManejadorAutenticacionPruebas.NombreEsquema;
            }).AddScheme<AuthenticationSchemeOptions, ManejadorAutenticacionPruebas>(
                ManejadorAutenticacionPruebas.NombreEsquema, _ => { });
            servicios.RemoveAll<GustosDbContext>();
            servicios.RemoveAll<DbContextOptions<GustosDbContext>>();
            servicios.AddDbContext<GustosDbContext>(opciones => opciones.UseInMemoryDatabase(_base));
            servicios.RemoveAll<IUsuarioRepository>();
            servicios.AddScoped<IUsuarioRepository, UsuarioRepositoryEF>();
            servicios.RemoveAll<IEmbeddingService>();
            servicios.AddSingleton<IEmbeddingService, EmbeddingReal>();
        });
    }

    public async Task PrepararAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
        await db.Database.EnsureCreatedAsync();
        string[] nombres = ["Pizza", "Pasta", "Tacos", "Empanadas", "Hamburguesa"];
        var catalogo = await db.Gustos.Where(g => nombres.Contains(g.Nombre)).ToListAsync();
        Gusto gusto(string nombre)
        {
            var existente = catalogo.SingleOrDefault(g => g.Nombre == nombre);
            if (existente != null) return existente;
            var nuevo = new Gusto { Id = Guid.NewGuid(), Nombre = nombre };
            db.Gustos.Add(nuevo);
            return nuevo;
        }
        var pizza = gusto("Pizza");
        var pasta = gusto("Pasta");
        var tacos = gusto("Tacos");
        var usuario = new Usuario(Uid, "filtros@example.test", "Usuario", "Filtros", "usuariofiltros")
            { RegistroInicialCompleto = true, Gustos = [pizza, gusto("Empanadas"), gusto("Hamburguesa")] };
        var amiga = new Usuario("amiga-filtros", "amiga@example.test", "Amiga Filtros", "Prueba", "amigafiltros") { RegistroInicialCompleto = true, Gustos = [pasta] };
        var pendiente = new Usuario("pendiente-filtros", "pendiente@example.test", "Pendiente", "Prueba", "pendientefiltros") { RegistroInicialCompleto = true, Gustos = [tacos] };
        db.AddRange(usuario, amiga, pendiente);
        var aceptada = new SolicitudAmistad(usuario.Id, amiga.Id);
        aceptada.Aceptar();
        db.AddRange(aceptada, new SolicitudAmistad(usuario.Id, pendiente.Id));
        Restaurante ficha(string nombre, Gusto plato, double lat, double lng, decimal? local, double? google) => new()
        {
            Id = Guid.NewGuid(), Nombre = nombre, NombreNormalizado = nombre.ToLowerInvariant(), PlaceId = "",
            Direccion = "Dirección de prueba", Latitud = lat, Longitud = lng,
            Valoracion = local, Rating = google, GustosQueSirve = [plato], MenuProcesado = false
        };
        db.Restaurantes.AddRange(
            ficha("Pizza local", pizza, 0.002, 0, 4.5m, 2),
            ficha("Pasta Google", pasta, 0.004, 0, null, 4.2),
            ficha("Tacos sin reseñas", tacos, 0.006, 0, null, null),
            ficha("Pizza local baja", pizza, 0.008, 0, 2, 5),
            ficha("Pizza al borde", pizza, 0.02697, 0, null, 4.5),
            ficha("Pasta fuera del radio", pasta, 0.052, 0, null, 5),
            ficha("Pizza esquina fuera", pizza, 0.026, 0.026, 4m, null));
        await db.SaveChangesAsync();
    }

    public async Task RevocarAmistadAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
        db.Remove(await db.SolicitudesAmistad.SingleAsync(s => s.Destinatario.FirebaseUid == "amiga-filtros"));
        await db.SaveChangesAsync();
    }

    public async Task QuitarPreferenciasAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
        var usuario = await db.Usuarios.Include(u => u.Gustos).SingleAsync(u => u.FirebaseUid == Uid);
        usuario.Gustos.Clear();
        await db.SaveChangesAsync();
    }

    private sealed class EmbeddingReal : IEmbeddingService, IDisposable
    {
        private readonly OnnxEmbeddingService _servicio = new(
            Path.Combine(AppContext.BaseDirectory, "ML", "model.onnx"),
            Path.Combine(AppContext.BaseDirectory, "ML", "tokenizer.json"));

        public float[] GetEmbedding(string text) => _servicio.GetEmbedding(text);
        public void Dispose() => _servicio.Dispose();
    }
}
