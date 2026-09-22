using FluentAssertions;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases.Importacion;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;

namespace GustosApp.Application.Tests;

public sealed class DescubrirRestaurantesCercanosUseCaseTests
{
    private readonly Mock<IBuscadorRestaurantesExternos> _buscador = new();
    private readonly Mock<IRestauranteRepository> _restauranteRepository = new();
    private readonly DescubrirRestaurantesCercanosUseCase _useCase;

    public DescubrirRestaurantesCercanosUseCaseTests()
    {
        _restauranteRepository
            .Setup(repository => repository.ObtenerPorPlaceIdsAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var importador = new ImportarRestaurantesUseCase(
            _restauranteRepository.Object,
            TimeProvider.System);
        _useCase = new DescubrirRestaurantesCercanosUseCase(_buscador.Object, importador);
    }

    [Fact]
    public async Task BuscarCercanos_DebeDevolverVistaPreviaSinPersistir()
    {
        var solicitud = new SolicitudDescubrimientoRestaurantes(-34.6037, -58.3816);
        _buscador
            .Setup(servicio => servicio.BuscarCercanosAsync(solicitud, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CrearEntrada()]);

        var resultado = await _useCase.HandleAsync(solicitud);

        resultado.Restaurantes.Should().ContainSingle();
        resultado.VistaPrevia.Confirmada.Should().BeFalse();
        resultado.VistaPrevia.Creados.Should().Be(1);
        resultado.VistaPrevia.Items.Single().Accion.Should().Be(AccionImportacionRestaurante.Crear);
        resultado.VistaPrevia.Items.Single().AccionNombre.Should().Be("Crear");
        _restauranteRepository.Verify(
            repository => repository.AddAsync(It.IsAny<Restaurante>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _restauranteRepository.Verify(
            repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(-91, -58, 1500, 20)]
    [InlineData(-34, 181, 1500, 20)]
    [InlineData(-34, -58, 0, 20)]
    [InlineData(-34, -58, 50_001, 20)]
    [InlineData(-34, -58, 1500, 0)]
    [InlineData(-34, -58, 1500, 21)]
    public async Task SolicitudFueraDeRango_NoDebeConsultarGoogle(
        double latitud,
        double longitud,
        int radioMetros,
        int cantidadMaxima)
    {
        var solicitud = new SolicitudDescubrimientoRestaurantes(
            latitud,
            longitud,
            radioMetros,
            cantidadMaxima);

        var accion = () => _useCase.HandleAsync(solicitud);

        await accion.Should().ThrowAsync<ArgumentException>();
        _buscador.Verify(
            servicio => servicio.BuscarCercanosAsync(
                It.IsAny<SolicitudDescubrimientoRestaurantes>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static RestauranteImportacionEntrada CrearEntrada() => new()
    {
        PlaceId = "place-nuevo",
        Nombre = "Restaurante nuevo",
        Direccion = "Av. Corrientes 1000",
        Latitud = -34.6037,
        Longitud = -58.3816,
        HorariosJson = "{}",
        PrimaryType = "restaurant",
        TypesJson = "[\"restaurant\"]"
    };
}
