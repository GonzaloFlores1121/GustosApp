using System.Net;
using System.Text;
using FluentAssertions;
using GustosApp.API.Extensiones;
using GustosApp.Application.Interfaces;
using GustosApp.Application.Services;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Application.UseCases.RestauranteUseCases.Importacion;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Moq;

namespace GustosApp.API.Tests;

public sealed class GeminiIntegracionPruebas
{
    private const string Modelo = "gemini-flash-latest";
    private const string Clave = "clave-ficticia";

    [Theory]
    [InlineData("", Modelo)]
    [InlineData(Clave, "")]
    public async Task SinConfiguracionCompleta_DiResuelveAmbosCasosYUsaCoincidenciasLocales(string clave, string modelo)
    {
        var pizza = new Gusto { Id = Guid.NewGuid(), Nombre = "Pizza" };
        var gustoGuardado = new Gusto { Id = Guid.NewGuid(), Nombre = "Pasta" };
        var restaurante = new Restaurante { Id = Guid.NewGuid(), Nombre = "Local", GustosQueSirve = [gustoGuardado] };
        var usuario = new Usuario { Id = Guid.NewGuid(), Gustos = [gustoGuardado] };
        var restaurantes = new Mock<IRestauranteRepository>();
        restaurantes.Setup(r => r.GetByIdAsync(restaurante.Id, It.IsAny<CancellationToken>())).ReturnsAsync(restaurante);
        var gustos = new Mock<IGustoRepository>();
        gustos.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([pizza, gustoGuardado]);
        var transporte = new Transporte((_, _) => throw new InvalidOperationException("No debe llamar al proveedor."));
        await using var servicios = CrearServicios(clave, modelo, transporte)
            .AgregarRedisCache(new ConfigurationBuilder().Build())
            .AddSingleton(TimeProvider.System)
            .AddSingleton(restaurantes.Object).AddSingleton(gustos.Object).AddSingleton(Mock.Of<IOcrService>())
            .AddScoped<EvaluadorCompatibilidadRestaurante>().AddScoped<RecomendacionIAUseCase>()
            .AddScoped<AnalizarMenuRestauranteImportadoUseCase>().BuildServiceProvider();
        await using var scope = servicios.CreateAsyncScope();

        var explicacion = await scope.ServiceProvider.GetRequiredService<RecomendacionIAUseCase>()
            .Handle(usuario, restaurante, default);
        var analisis = await scope.ServiceProvider.GetRequiredService<AnalizarMenuRestauranteImportadoUseCase>()
            .HandleAsync(restaurante.Id, "Pizza napolitana", []);

        explicacion.Should().Contain("coinciden con tus gustos: Pasta");
        analisis.AnalizadoConIa.Should().BeFalse();
        analisis.GustosSugeridos.Select(g => g.Id).Should().Equal(pizza.Id);
        restaurante.GustosQueSirve.Should().Equal(gustoGuardado);
        transporte.Peticiones.Should().Be(0);
    }

    [Fact]
    public async Task Generar_UsaElTransporteInyectadoYUneLasPartesPublicas()
    {
        var transporte = new Transporte(async (peticion, ct) =>
        {
            peticion.RequestUri!.Host.Should().Be("generativelanguage.googleapis.com");
            peticion.RequestUri.AbsolutePath.Should().EndWith($"/{Modelo}:generateContent");
            peticion.RequestUri.Query.Should().NotContain(Clave);
            peticion.Headers.GetValues("x-goog-api-key").Should().Equal(Clave);
            (await peticion.Content!.ReadAsStringAsync(ct)).Should().Contain("texto de prueba");
            return Json("""
                {"candidates":[{"finishReason":"STOP","content":{"parts":[
                  {"thought":true,"text":"interno"},{"text":"Gustos "},{"text":"compartidos."}]}}]}
                """);
        });
        await using var servicios = CrearServicios("", Modelo, transporte, Clave).BuildServiceProvider();
        var ia = servicios.GetRequiredService<IRecomendacionAIService>();

        (await ia.GenerarRecomendacion("texto de prueba", default)).Should().Be("Gustos compartidos.");
        (await ia.GenerarRecomendacion("texto de prueba", default)).Should().Be("Gustos compartidos.");
        transporte.Peticiones.Should().Be(2);
    }

    [Fact]
    public async Task Explicacion_RedisInaccesible_DiResuelveYDevuelveCoincidenciasSinLlamarAGemini()
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Redis"] = "127.0.0.1:1" }).Build();
        var transporte = new Transporte((_, _) => throw new InvalidOperationException("No debe llamar al proveedor."));
        await using var servicios = CrearServicios(Clave, Modelo, transporte)
            .AgregarRedisCache(configuracion).AddSingleton(TimeProvider.System)
            .AddScoped<EvaluadorCompatibilidadRestaurante>().AddScoped<RecomendacionIAUseCase>().BuildServiceProvider();
        await using var scope = servicios.CreateAsyncScope();
        var gusto = new Gusto { Id = Guid.NewGuid(), Nombre = "Pizza" };
        var usuario = new Usuario { Gustos = [gusto] };
        var restaurante = new Restaurante { Id = Guid.NewGuid(), Nombre = "Restaurante", GustosQueSirve = [gusto] };

        var explicacion = await scope.ServiceProvider.GetRequiredService<RecomendacionIAUseCase>()
            .Handle(usuario, restaurante, default).WaitAsync(TimeSpan.FromSeconds(5));

        explicacion.Should().Contain("coinciden con tus gustos: Pizza");
        transporte.Peticiones.Should().Be(0);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(503)]
    public async Task Generar_ErrorDelProveedor_DevuelveRespaldoSinReintentos(int status)
    {
        var transporte = new Transporte((_, _) => Task.FromResult(
            Json($$$"""{"error":{"code":{{{status}}},"message":"detalle privado"}}""", (HttpStatusCode)status)));
        await using var servicios = CrearServicios(Clave, Modelo, transporte).BuildServiceProvider();

        (await servicios.GetRequiredService<IRecomendacionAIService>().GenerarRecomendacion("menú", default)).Should().BeNull();
        transporte.Peticiones.Should().Be(1);
    }

    [Theory]
    [InlineData("""{"candidates":[]}""")]
    [InlineData("""{"promptFeedback":{"blockReason":"SAFETY"}}""")]
    [InlineData("""{"candidates":[{"finishReason":"SAFETY","content":{"parts":[{"text":"bloqueado"}]}}]}""")]
    public async Task Generar_SinRespuestaPublicable_DevuelveRespaldo(string json)
    {
        var transporte = new Transporte((_, _) => Task.FromResult(Json(json)));
        await using var servicios = CrearServicios(Clave, Modelo, transporte).BuildServiceProvider();

        (await servicios.GetRequiredService<IRecomendacionAIService>().GenerarRecomendacion("menú", default)).Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Generar_CancelaElTransporteYSoloUsaRespaldoAlVencerElTiempo(bool cancelaUsuario)
    {
        using var solicitud = new CancellationTokenSource();
        var transporte = new Transporte(async (_, ct) =>
        {
            if (cancelaUsuario) solicitud.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException();
        });
        await using var servicios = CrearServicios(Clave, Modelo, transporte).BuildServiceProvider();
        var ia = servicios.GetRequiredService<IRecomendacionAIService>();

        var tarea = ia.GenerarRecomendacion("menú", solicitud.Token).WaitAsync(TimeSpan.FromSeconds(8));
        if (cancelaUsuario)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await tarea);
        else
            (await tarea).Should().BeNull();
        transporte.Peticiones.Should().Be(1);
    }

    [Fact]
    public async Task Explicacion_AplicaLimiteConfiguradoYMuestraComoReintentar()
    {
        using var fabrica = new FabricaApiGustosAppAutenticada();
        using var app = fabrica.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["GeminiSettings:RecomendacionesPorMinuto"] = "1" }));
            builder.ConfigureTestServices(servicios =>
            {
                servicios.RemoveAll<IRestauranteRepository>();
                servicios.AddSingleton(Mock.Of<IRestauranteRepository>());
            });
        });
        using var cliente = app.CreateClient();
        var ruta = $"/api/Recomendador/{Guid.NewGuid()}/recomendacion";
        using var primera = await cliente.GetAsync(ruta);
        using var posterior = await cliente.GetAsync(ruta);

        primera.StatusCode.Should().Be(HttpStatusCode.NotFound);
        posterior.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await posterior.Content.ReadAsStringAsync()).Should().Contain("Probá de nuevo en un minuto");
    }

    private static IServiceCollection CrearServicios(string clave, string modelo, Transporte transporte, string? claveAlternativa = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GeminiSettings:ApiKey"] = clave,
            ["GeminiSettings:Model"] = modelo,
            ["GeminiSettings:TimeoutSegundos"] = "1",
            ["GOOGLE_API_KEY"] = claveAlternativa
        }).Build();
        var servicios = new ServiceCollection().AddLogging().AgregarGeminiIntegracion(config);
        servicios.Configure<HttpClientFactoryOptions>(nameof(IRecomendacionAIService),
            opciones => opciones.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = transporte));
        return servicios;
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class Transporte(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public int Peticiones { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Peticiones++;
            return responder(request, cancellationToken);
        }
    }
}
