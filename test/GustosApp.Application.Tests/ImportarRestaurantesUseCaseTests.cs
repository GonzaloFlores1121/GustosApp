using FluentAssertions;
using GustosApp.Application.UseCases.RestauranteUseCases.Importacion;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;

namespace GustosApp.Application.Tests;

public class ImportarRestaurantesUseCaseTests
{
    private static readonly DateTime FechaActualUtc = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);
    private readonly Mock<IRestauranteRepository> _restauranteRepository = new();
    private readonly ImportarRestaurantesUseCase _useCase;

    public ImportarRestaurantesUseCaseTests()
    {
        var timeProvider = new Mock<TimeProvider>();
        timeProvider
            .Setup(provider => provider.GetUtcNow())
            .Returns(new DateTimeOffset(FechaActualUtc));

        _useCase = new ImportarRestaurantesUseCase(
            _restauranteRepository.Object,
            timeProvider.Object);
    }

    [Fact]
    public async Task VistaPrevia_NoDebePersistirCambios()
    {
        var entrada = CrearEntrada();
        PrepararExistentes();

        var resultado = await _useCase.HandleAsync([entrada], confirmar: false);

        resultado.Confirmada.Should().BeFalse();
        resultado.Creados.Should().Be(1);
        resultado.Items.Single().Accion.Should().Be(AccionImportacionRestaurante.Crear);
        _restauranteRepository.Verify(
            repository => repository.AddAsync(It.IsAny<Restaurante>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _restauranteRepository.Verify(
            repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Confirmar_RestauranteNuevo_DebeCrearloComoFichaSinPropietario()
    {
        var entrada = CrearEntrada();
        Restaurante? agregado = null;
        PrepararExistentes();
        _restauranteRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Restaurante>(), It.IsAny<CancellationToken>()))
            .Callback<Restaurante, CancellationToken>((restaurante, _) => agregado = restaurante)
            .Returns(Task.CompletedTask);
        _restauranteRepository
            .Setup(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var resultado = await _useCase.HandleAsync([entrada], confirmar: true);

        resultado.Creados.Should().Be(1);
        agregado.Should().NotBeNull();
        agregado!.PlaceId.Should().Be(entrada.PlaceId);
        agregado.DuenoId.Should().BeNull();
        agregado.PropietarioUid.Should().BeEmpty();
        agregado.Rating.Should().Be(4.5);
        _restauranteRepository.Verify(
            repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RepetirMismosDatos_NoDebeActualizarNiGuardar()
    {
        var entrada = CrearEntrada(fechaDatosUtc: null);
        var existente = CrearRestauranteExistente();
        PrepararExistentes(existente);

        var resultado = await _useCase.HandleAsync([entrada], confirmar: true);

        resultado.SinCambios.Should().Be(1);
        resultado.Items.Single().Accion.Should().Be(AccionImportacionRestaurante.SinCambios);
        _restauranteRepository.Verify(
            repository => repository.UpdateAsync(It.IsAny<Restaurante>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _restauranteRepository.Verify(
            repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RestauranteConPropietario_NoDebeSobrescribirloAutomaticamente()
    {
        var existente = CrearRestauranteExistente();
        existente.DuenoId = Guid.NewGuid();
        PrepararExistentes(existente);

        var resultado = await _useCase.HandleAsync(
            [CrearEntrada(nombre: "Nombre importado nuevo")],
            confirmar: true);

        resultado.Omitidos.Should().Be(1);
        resultado.Items.Single().Accion.Should().Be(AccionImportacionRestaurante.RequiereRevision);
        existente.Nombre.Should().Be("Restaurante de prueba");
        _restauranteRepository.Verify(
            repository => repository.UpdateAsync(It.IsAny<Restaurante>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DatosMasAntiguos_NoDebenReemplazarDatosRecientes()
    {
        var existente = CrearRestauranteExistente();
        PrepararExistentes(existente);
        var fechaAnterior = existente.UltimaActualizacion.AddDays(-1);

        var resultado = await _useCase.HandleAsync(
            [CrearEntrada(nombre: "Nombre viejo", fechaDatosUtc: fechaAnterior)],
            confirmar: true);

        resultado.Items.Single().Accion.Should().Be(AccionImportacionRestaurante.IgnorarPorAntiguedad);
        existente.Nombre.Should().Be("Restaurante de prueba");
        _restauranteRepository.Verify(
            repository => repository.UpdateAsync(It.IsAny<Restaurante>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PlaceIdRepetidoEnLote_DebeProcesarSoloLaPrimeraEntrada()
    {
        PrepararExistentes();

        var resultado = await _useCase.HandleAsync(
            [CrearEntrada(), CrearEntrada(nombre: "Duplicado")],
            confirmar: false);

        resultado.Total.Should().Be(2);
        resultado.Creados.Should().Be(1);
        resultado.Omitidos.Should().Be(1);
        resultado.Items.Last().Accion.Should().Be(AccionImportacionRestaurante.DuplicadoEnLote);
    }

    [Fact]
    public async Task ActualizacionParcial_NoDebeBorrarDatosOpcionalesExistentes()
    {
        var existente = CrearRestauranteExistente();
        existente.WebUrl = "https://restaurante.example";
        existente.HorariosJson = "{\"lunes\":\"20:00-23:00\"}";
        existente.TypesJson = "[\"restaurant\",\"pizza_restaurant\"]";
        PrepararExistentes(existente);
        _restauranteRepository
            .Setup(repository => repository.UpdateAsync(existente, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _restauranteRepository
            .Setup(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var entrada = CrearEntrada(nombre: "Nombre actualizado", fechaDatosUtc: FechaActualUtc) with
        {
            WebUrl = null,
            ImagenUrl = null,
            Rating = null,
            CantidadResenas = null,
            HorariosJson = "{}",
            TypesJson = "[]"
        };

        var resultado = await _useCase.HandleAsync([entrada], confirmar: true);

        resultado.Actualizados.Should().Be(1);
        existente.WebUrl.Should().Be("https://restaurante.example");
        existente.ImagenUrl.Should().Be("https://example.com/restaurante.jpg");
        existente.Rating.Should().Be(4.5);
        existente.CantidadResenas.Should().Be(120);
        existente.HorariosJson.Should().Be("{\"lunes\":\"20:00-23:00\"}");
        existente.TypesJson.Should().Be("[\"restaurant\",\"pizza_restaurant\"]");
    }

    private void PrepararExistentes(params Restaurante[] restaurantes)
    {
        _restauranteRepository
            .Setup(repository => repository.ObtenerPorPlaceIdsAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurantes.ToList());
    }

    private static RestauranteImportacionEntrada CrearEntrada(
        string nombre = "Restaurante de prueba",
        DateTime? fechaDatosUtc = null) => new()
        {
            PlaceId = "google-place-1",
            Nombre = nombre,
            Direccion = "Av. Siempre Viva 123",
            Latitud = -34.60,
            Longitud = -58.50,
            HorariosJson = "{}",
            Rating = 4.5,
            CantidadResenas = 120,
            Categoria = "restaurant",
            FechaDatosUtc = fechaDatosUtc,
            PrimaryType = "restaurant",
            TypesJson = "[\"restaurant\"]",
            ImagenUrl = "https://example.com/restaurante.jpg"
        };

    private static Restaurante CrearRestauranteExistente() => new()
    {
        Id = Guid.NewGuid(),
        PlaceId = "google-place-1",
        Nombre = "Restaurante de prueba",
        NombreNormalizado = "restaurante de prueba",
        Direccion = "Av. Siempre Viva 123",
        Latitud = -34.60,
        Longitud = -58.50,
        HorariosJson = "{}",
        Rating = 4.5,
        CantidadResenas = 120,
        Categoria = "restaurant",
        PrimaryType = "restaurant",
        TypesJson = "[\"restaurant\"]",
        ImagenUrl = "https://example.com/restaurante.jpg",
        ActualizadoUtc = FechaActualUtc.AddDays(-1),
        UltimaActualizacion = FechaActualUtc.AddDays(-1)
    };
}
