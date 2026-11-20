using FluentAssertions;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;
using GustosApp.Application.Services;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Application.UseCases.UsuarioUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;

namespace GustosApp.Application.Tests;

public class BuscarRestaurantesRecomendadosOrquestadorTests
{
    [Fact]
    public async Task FiltrosExplicitos_AplicanOrSinReemplazarLasPreferenciasDelPerfil()
    {
        var pizza = CrearGusto("Pizza");
        var cafe = CrearGusto("Café con leche");
        var sushi = CrearGusto("Sushi");
        var usuarioRepo = new Mock<IUsuarioRepository>();
        usuarioRepo.Setup(r => r.GetByFirebaseUidAsync("uid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Usuario { Gustos = [pizza] });
        var servicio = new Mock<IServicioRestaurantes>();
        servicio.Setup(s => s.BuscarAsync(
                0, null, null, 3000,
                It.Is<List<string>>(g => g.SequenceEqual(new[] { "Pizza" })),
                It.IsAny<List<string>>()))
            .ReturnsAsync([
                CrearRestaurante("Pizzería", pizza),
                CrearRestaurante("Café", cafe),
                CrearRestaurante("Sushi", sushi)
            ]);
        var catalogo = new Mock<IGustoRepository>();
        catalogo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([pizza, cafe, sushi]);
        var restauranteRepo = new Mock<IRestauranteRepository>();
        restauranteRepo.Setup(r => r.obtenerRestauranteConResenias(It.IsAny<List<Guid>>()))
            .ReturnsAsync([]);
        var estadisticas = new Mock<IRestauranteEstadisticasRepository>();
        estadisticas.Setup(r => r.IncrementarTop3IndividualAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var orquestador = CrearOrquestador(
            usuarioRepo,
            servicio,
            catalogo,
            restauranteRepo,
            estadisticas);

        var resultado = await orquestador.HandleAsync(
            "uid", ["Café con leche", "Sushi"], null, null, null, 3000, 10, 0, default);

        resultado.Select(r => r.Nombre).Should().BeEquivalentTo(["Café", "Sushi"]);
        servicio.VerifyAll();
    }

    [Fact]
    public async Task GustoExplicitoInexistente_DevuelveUnErrorTipado()
    {
        var pizza = CrearGusto("Pizza");
        var usuarioRepo = new Mock<IUsuarioRepository>();
        var catalogo = new Mock<IGustoRepository>();
        catalogo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([pizza]);
        var orquestador = CrearOrquestador(
            usuarioRepo,
            new Mock<IServicioRestaurantes>(),
            catalogo,
            new Mock<IRestauranteRepository>(),
            new Mock<IRestauranteEstadisticasRepository>());

        var accion = () => orquestador.HandleAsync(
            "uid", ["No existe"], null, null, null, 3000, 10, 0, default);

        await accion.Should().ThrowAsync<FiltroGustosInvalidoException>();
    }

    [Fact]
    public async Task SinPreferenciasNiFiltro_DevuelveUnEstadoTipado()
    {
        var usuarioRepo = new Mock<IUsuarioRepository>();
        usuarioRepo.Setup(r => r.GetByFirebaseUidAsync("uid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Usuario());
        var catalogo = new Mock<IGustoRepository>();
        var orquestador = CrearOrquestador(
            usuarioRepo,
            new Mock<IServicioRestaurantes>(),
            catalogo,
            new Mock<IRestauranteRepository>(),
            new Mock<IRestauranteEstadisticasRepository>());

        var accion = () => orquestador.HandleAsync(
            "uid", null, null, null, null, 3000, 10, 0, default);

        await accion.Should().ThrowAsync<SinPreferenciasRecomendacionException>();
    }

    private static BuscarRestaurantesRecomendadosOrquestador CrearOrquestador(
        Mock<IUsuarioRepository> usuarioRepo,
        Mock<IServicioRestaurantes> servicio,
        Mock<IGustoRepository> catalogo,
        Mock<IRestauranteRepository> restauranteRepo,
        Mock<IRestauranteEstadisticasRepository> estadisticas)
    {
        var preferenciasIndividuales = new ConstruirPreferenciasUsuarioIndividualUseCase(usuarioRepo.Object);
        var preferenciasConAmigo = new ConstruirPreferenciasUsuarioConAmigoCase(
            usuarioRepo.Object,
            Mock.Of<ISolicitudAmistadRepository>(),
            Mock.Of<IUsuarioPreferenciasService>());
        var sugerir = new SugerirGustosSobreUnRadioUseCase(
            new EmbeddingConstante(),
            restauranteRepo.Object,
            new EvaluadorCompatibilidadRestaurante());

        return new BuscarRestaurantesRecomendadosOrquestador(
            preferenciasIndividuales,
            preferenciasConAmigo,
            servicio.Object,
            sugerir,
            Mock.Of<ICacheService>(),
            new RegistrarTop3IndividualRestaurantesUseCase(estadisticas.Object),
            catalogo.Object);
    }

    private static Gusto CrearGusto(string nombre) => new()
    {
        Id = Guid.NewGuid(),
        Nombre = nombre
    };

    private static Restaurante CrearRestaurante(string nombre, Gusto gusto) => new()
    {
        Id = Guid.NewGuid(),
        Nombre = nombre,
        PlaceId = Guid.NewGuid().ToString("N"),
        GustosQueSirve = [gusto]
    };

    private sealed class EmbeddingConstante : IEmbeddingService
    {
        public float[] GetEmbedding(string text) => [1, 1, 1];
    }
}
