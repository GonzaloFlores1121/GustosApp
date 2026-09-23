using FluentAssertions;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases.Importacion;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;

namespace GustosApp.Application.Tests;

public sealed class GestionarMenuRestauranteImportadoUseCasesTests
{
    [Fact]
    public async Task Analizar_DebeAceptarSoloGustosExistentesDelCatalogo()
    {
        var restaurante = CrearRestauranteImportado();
        var pizza = new Gusto { Id = Guid.NewGuid(), Nombre = "Pizza" };
        var restaurantes = new Mock<IRestauranteRepository>();
        restaurantes.Setup(r => r.GetByIdAsync(restaurante.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurante);
        var gustos = new Mock<IGustoRepository>();
        gustos.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([pizza]);
        var ia = new Mock<IRecomendacionAIService>();
        ia.Setup(s => s.GenerarRecomendacion(It.IsAny<string>()))
            .ReturnsAsync("{\"categoria\":\"Pizzería\",\"gustos\":[\"Pizza\",\"Inventado\"]}");

        var useCase = new AnalizarMenuRestauranteImportadoUseCase(
            restaurantes.Object,
            gustos.Object,
            Mock.Of<IOcrService>(),
            ia.Object,
            NullLogger<AnalizarMenuRestauranteImportadoUseCase>.Instance);

        var resultado = await useCase.HandleAsync(
            restaurante.Id,
            "Muzzarella grande",
            [],
            CancellationToken.None);

        resultado.CategoriaSugerida.Should().Be("Pizzería");
        resultado.GustosSugeridos.Should().ContainSingle().Which.Nombre.Should().Be("Pizza");
        resultado.CatalogoGustos.Should().ContainSingle().Which.Nombre.Should().Be("Pizza");
        resultado.AnalizadoConIa.Should().BeTrue();
        resultado.DetalleAnalisis.Should().Contain("Gemini complementó");
    }

    [Fact]
    public async Task Analizar_SinIa_DebeReconocerEquivalenciasDeCafeteriaYExplicarElResultado()
    {
        var restaurante = CrearRestauranteImportado();
        var cafeConLeche = new Gusto { Id = Guid.NewGuid(), Nombre = "Café con leche" };
        var pasteleria = new Gusto { Id = Guid.NewGuid(), Nombre = "Pastelería" };
        var restaurantes = new Mock<IRestauranteRepository>();
        restaurantes.Setup(r => r.GetByIdAsync(restaurante.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurante);
        var gustos = new Mock<IGustoRepository>();
        gustos.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([cafeConLeche, pasteleria]);
        var ia = new Mock<IRecomendacionAIService>();
        ia.Setup(s => s.GenerarRecomendacion(It.IsAny<string>()))
            .ReturnsAsync("Error de límite de uso");
        var useCase = new AnalizarMenuRestauranteImportadoUseCase(
            restaurantes.Object, gustos.Object, Mock.Of<IOcrService>(), ia.Object,
            NullLogger<AnalizarMenuRestauranteImportadoUseCase>.Instance);

        var resultado = await useCase.HandleAsync(
            restaurante.Id,
            "Flat White con leche microtexturada. Croissant clásico de manteca.",
            []);

        resultado.AnalizadoConIa.Should().BeFalse();
        resultado.GustosSugeridos.Select(g => g.Nombre).Should().BeEquivalentTo("Café con leche", "Pastelería");
        resultado.DetalleAnalisis.Should().Contain("no estuvo disponible");
    }

    [Fact]
    public async Task Analizar_FalloDeGemini_ConservaAnalisisLocalYNoExponeElErrorTecnico()
    {
        var restaurante = CrearRestauranteImportado();
        var pizza = new Gusto { Id = Guid.NewGuid(), Nombre = "Pizza" };
        restaurante.GustosQueSirve.Add(pizza);
        var restaurantes = new Mock<IRestauranteRepository>();
        restaurantes.Setup(r => r.GetByIdAsync(restaurante.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurante);
        var gustos = new Mock<IGustoRepository>();
        gustos.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([pizza]);
        var ia = new Mock<IRecomendacionAIService>();
        ia.Setup(s => s.GenerarRecomendacion(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("API key secreta inválida"));
        var useCase = new AnalizarMenuRestauranteImportadoUseCase(
            restaurantes.Object, gustos.Object, Mock.Of<IOcrService>(), ia.Object,
            NullLogger<AnalizarMenuRestauranteImportadoUseCase>.Instance);

        var resultado = await useCase.HandleAsync(restaurante.Id, "Pizza napolitana", []);

        resultado.AnalizadoConIa.Should().BeFalse();
        resultado.GustosSugeridos.Should().ContainSingle().Which.Id.Should().Be(pizza.Id);
        resultado.DetalleAnalisis.Should().Contain("no estuvo disponible");
        resultado.DetalleAnalisis.Should().NotContain("API key");
        restaurante.GustosQueSirve.Should().ContainSingle().Which.Should().BeSameAs(pizza);
    }

    [Fact]
    public async Task Analizar_SugerenciasNuevas_NoModificaLosGustosPersistidosAntesDeConfirmar()
    {
        var restaurante = CrearRestauranteImportado();
        var pizza = new Gusto { Id = Guid.NewGuid(), Nombre = "Pizza" };
        var empanadas = new Gusto { Id = Guid.NewGuid(), Nombre = "Empanadas" };
        restaurante.GustosQueSirve.Add(pizza);
        var restaurantes = new Mock<IRestauranteRepository>();
        restaurantes.Setup(r => r.GetByIdAsync(restaurante.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurante);
        var gustos = new Mock<IGustoRepository>();
        gustos.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([pizza, empanadas]);
        var ia = new Mock<IRecomendacionAIService>();
        ia.Setup(s => s.GenerarRecomendacion(It.IsAny<string>()))
            .ReturnsAsync("{\"categoria\":\"Restaurante\",\"gustos\":[\"Empanadas\"]}");
        var useCase = new AnalizarMenuRestauranteImportadoUseCase(
            restaurantes.Object, gustos.Object, Mock.Of<IOcrService>(), ia.Object,
            NullLogger<AnalizarMenuRestauranteImportadoUseCase>.Instance);

        var resultado = await useCase.HandleAsync(restaurante.Id, "Empanadas de carne", []);

        resultado.GustosSugeridos.Should().Contain(gusto => gusto.Id == empanadas.Id);
        restaurante.GustosQueSirve.Should().ContainSingle().Which.Should().BeSameAs(pizza);
        restaurantes.Verify(r => r.UpdateAsync(It.IsAny<Restaurante>(), It.IsAny<CancellationToken>()), Times.Never);
        restaurantes.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Pendientes_DebeInformarTodosLosMotivosDeRevision()
    {
        var restaurante = CrearRestauranteImportado();
        restaurante.MenuProcesado = false;
        restaurante.MenuError = "Imagen ilegible";
        restaurante.RegistrarDatosCompatibilidadEstimados(
            OrigenDatosCompatibilidadRestaurante.GooglePlaces,
            DateTime.UtcNow);
        var repositorio = new Mock<IRestauranteRepository>();
        repositorio.Setup(r => r.ObtenerPendientesClasificacionAsync(
                ObtenerPendientesClasificacionRestauranteUseCase.CantidadMaxima,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([restaurante]);
        var useCase = new ObtenerPendientesClasificacionRestauranteUseCase(repositorio.Object);

        var resultado = await useCase.HandleAsync();

        resultado.Should().ContainSingle();
        resultado.Single().Motivos.Should().Equal(
            "Error al procesar menú",
            "Sin gustos",
            "Clasificación automática");
    }

    [Fact]
    public async Task ObtenerDetalle_SinMenu_DebeIncluirGustosActualesYCatalogoCompleto()
    {
        var restaurante = CrearRestauranteImportado();
        var pizza = new Gusto { Id = Guid.NewGuid(), Nombre = "Pizza" };
        var empanadas = new Gusto { Id = Guid.NewGuid(), Nombre = "Empanadas" };
        restaurante.GustosQueSirve.Add(pizza);
        var restaurantes = new Mock<IRestauranteRepository>();
        restaurantes.Setup(r => r.GetByIdAsync(restaurante.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurante);
        var gustos = new Mock<IGustoRepository>();
        gustos.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([pizza, empanadas]);
        var useCase = new ObtenerDetalleGestionRestauranteUseCase(restaurantes.Object, gustos.Object);

        var resultado = await useCase.HandleAsync(restaurante.Id);

        resultado.EstadoMenu.Should().Be("Sin procesar");
        resultado.Gustos.Should().ContainSingle().Which.Id.Should().Be(pizza.Id);
        resultado.CatalogoGustos.Select(gusto => gusto.Id).Should()
            .BeEquivalentTo(new[] { pizza.Id, empanadas.Id });
    }

    [Fact]
    public async Task GuardarClasificacion_SinMenu_DebeQuitarSoloGustosDesmarcadosYSinCambiarEstadoMenu()
    {
        var restaurante = CrearRestauranteImportado();
        restaurante.MenuProcesado = false;
        restaurante.MenuError = "Error anterior";
        var pizza = new Gusto { Id = Guid.NewGuid(), Nombre = "Pizza" };
        var empanadas = new Gusto { Id = Guid.NewGuid(), Nombre = "Empanadas" };
        var cafe = new Gusto { Id = Guid.NewGuid(), Nombre = "Café" };
        restaurante.GustosQueSirve.Add(pizza);
        restaurante.GustosQueSirve.Add(empanadas);
        var restaurantes = new Mock<IRestauranteRepository>();
        restaurantes.Setup(r => r.GetByIdAsync(restaurante.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurante);
        var gustos = new Mock<IGustoRepository>();
        gustos.Setup(r => r.GetByIdsAsync(
                It.Is<List<Guid>>(ids => ids.Contains(pizza.Id) && ids.Contains(cafe.Id)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([pizza, cafe]);
        var useCase = new GuardarClasificacionRestauranteImportadoUseCase(
            restaurantes.Object, gustos.Object, TimeProvider.System);

        await useCase.HandleAsync(restaurante.Id, "Cafetería", [pizza.Id, cafe.Id]);

        restaurante.GustosQueSirve.Should().BeEquivalentTo([pizza, cafe]);
        restaurante.MenuProcesado.Should().BeFalse();
        restaurante.MenuError.Should().Be("Error anterior");
        restaurantes.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GuardarClasificacion_RestauranteConPropietario_DebeRechazarLaEdicion()
    {
        var restaurante = CrearRestauranteImportado();
        restaurante.PropietarioUid = "propietario";
        var restaurantes = new Mock<IRestauranteRepository>();
        restaurantes.Setup(r => r.GetByIdAsync(restaurante.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurante);
        var useCase = new GuardarClasificacionRestauranteImportadoUseCase(
            restaurantes.Object, Mock.Of<IGustoRepository>(), TimeProvider.System);

        var accion = () => useCase.HandleAsync(restaurante.Id, "Restaurante", []);

        await accion.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*sólo para restaurantes importados*");
    }

    [Fact]
    public async Task Confirmar_DebeGuardarMenuYGustosSinInventarRestricciones()
    {
        var restaurante = CrearRestauranteImportado();
        var restriccion = new Restriccion { Id = Guid.NewGuid(), Nombre = "Sin gluten" };
        restaurante.RestriccionesQueRespeta.Add(restriccion);
        var pizza = new Gusto { Id = Guid.NewGuid(), Nombre = "Pizza" };
        var restaurantes = new Mock<IRestauranteRepository>();
        restaurantes.Setup(r => r.GetByIdAsync(restaurante.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurante);
        var gustos = new Mock<IGustoRepository>();
        gustos.Setup(r => r.GetByIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pizza]);
        var menus = new Mock<IRestauranteMenuRepository>();
        menus.Setup(r => r.GetByRestauranteIdAsync(restaurante.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RestauranteMenu?)null);
        var parser = new Mock<IMenuParser>();
        parser.Setup(p => p.ParsearAsync(It.IsAny<string>(), "ARS", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"categorias\":[]}");
        var reloj = new Mock<TimeProvider>();
        reloj.Setup(r => r.GetUtcNow()).Returns(new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero));
        var useCase = new ConfirmarMenuRestauranteImportadoUseCase(
            restaurantes.Object, gustos.Object, menus.Object, parser.Object, reloj.Object);

        await useCase.HandleAsync(restaurante.Id, "Pizza muzzarella", "Pizzería", [pizza.Id]);

        restaurante.Categoria.Should().Be("Pizzería");
        restaurante.GustosQueSirve.Should().ContainSingle().Which.Should().BeSameAs(pizza);
        restaurante.RestriccionesQueRespeta.Should().ContainSingle().Which.Should().BeSameAs(restriccion);
        restaurante.OrigenDatosCompatibilidad.Should().Be(OrigenDatosCompatibilidadRestaurante.Administracion);
        restaurante.EstadoDatosCompatibilidad.Should().Be(EstadoDatosCompatibilidadRestaurante.Estimado);
        restaurante.MenuProcesado.Should().BeTrue();
        menus.Verify(r => r.AddAsync(It.Is<RestauranteMenu>(m => m.RestauranteId == restaurante.Id), It.IsAny<CancellationToken>()));
        restaurantes.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Confirmar_RestauranteConPropietario_DebeRechazarLaEdicionAdministrativa()
    {
        var restaurante = CrearRestauranteImportado();
        restaurante.DuenoId = Guid.NewGuid();
        var restaurantes = new Mock<IRestauranteRepository>();
        restaurantes.Setup(r => r.GetByIdAsync(restaurante.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurante);
        var useCase = new ConfirmarMenuRestauranteImportadoUseCase(
            restaurantes.Object,
            Mock.Of<IGustoRepository>(),
            Mock.Of<IRestauranteMenuRepository>(),
            Mock.Of<IMenuParser>(),
            TimeProvider.System);

        var accion = () => useCase.HandleAsync(restaurante.Id, "Menú", "Restaurante", []);

        await accion.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*sólo para restaurantes importados*");
    }

    private static Restaurante CrearRestauranteImportado() => new()
    {
        Id = Guid.NewGuid(),
        Nombre = "Restaurante importado",
        NombreNormalizado = "restaurante importado",
        PlaceId = "place-id",
        PropietarioUid = string.Empty
    };
}
