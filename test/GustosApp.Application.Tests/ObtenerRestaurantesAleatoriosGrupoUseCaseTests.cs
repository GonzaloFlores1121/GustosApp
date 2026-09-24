using GustosApp.Application.Interfaces;
using GustosApp.Application.Model;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;
using Xunit;

namespace GustosApp.Application.Tests;

public class ObtenerRestaurantesAleatoriosGrupoUseCaseTests
{
    private const string FirebaseUid = "usuario-1";
    private readonly Mock<IGrupoRepository> _grupoRepository = new();
    private readonly Mock<IBuscadorRestaurantesGrupo> _buscador = new();

    [Fact]
    public async Task HandleAsync_GrupoNoExiste_LanzaArgumentException()
    {
        var grupoId = Guid.NewGuid();
        var sut = CrearCasoDeUso();
        _grupoRepository
            .Setup(repository => repository.GetByIdAsync(grupoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Grupo?)null);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.HandleAsync(
            FirebaseUid,
            grupoId,
            CrearRequest(),
            CancellationToken.None));

        _buscador.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_SinOpcionesCompatibles_DevuelveListaVacia()
    {
        var grupoId = Guid.NewGuid();
        var sut = CrearCasoDeUso();
        ConfigurarGrupo(grupoId);
        _buscador
            .Setup(buscador => buscador.BuscarAsync(
                FirebaseUid, grupoId, null, null, null, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoBusquedaRestaurantesGrupo(
                EstadoBusquedaRestaurantesGrupo.SinOpciones,
                Array.Empty<Restaurante>()));

        var resultado = await sut.HandleAsync(
            FirebaseUid, grupoId, CrearRequest(), CancellationToken.None);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task HandleAsync_UsaLaMismaBusquedaFiltradaYRespetaLaCantidad()
    {
        var grupoId = Guid.NewGuid();
        var sut = CrearCasoDeUso();
        var restaurantes = new[]
        {
            CrearRestaurante("Uno"),
            CrearRestaurante("Dos"),
            CrearRestaurante("Tres")
        };
        ConfigurarGrupo(grupoId);
        _buscador
            .Setup(buscador => buscador.BuscarAsync(
                FirebaseUid, grupoId, -34.6, -58.4, 1500, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoBusquedaRestaurantesGrupo(
                EstadoBusquedaRestaurantesGrupo.Encontrados,
                restaurantes));

        var resultado = await sut.HandleAsync(
            FirebaseUid,
            grupoId,
            CrearRequest(2, -34.6, -58.4, 1500),
            CancellationToken.None);

        Assert.Equal(2, resultado.Count);
        Assert.All(resultado, item => Assert.Contains(item.Id, restaurantes.Select(r => r.Id)));
    }

    private ObtenerRestaurantesAleatoriosGrupoUseCase CrearCasoDeUso() =>
        new(_grupoRepository.Object, _buscador.Object);

    private void ConfigurarGrupo(Guid grupoId)
    {
        _grupoRepository
            .Setup(repository => repository.GetByIdAsync(grupoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Grupo("Grupo", Guid.NewGuid()) { Id = grupoId });
    }

    private static ObtenerRestaurantesAleatoriosRequestModel CrearRequest(
        int cantidad = 3,
        double? latitud = null,
        double? longitud = null,
        int? radioMetros = null) => new()
        {
            Cantidad = cantidad,
            Latitud = latitud,
            Longitud = longitud,
            RadioMetros = radioMetros
        };

    private static Restaurante CrearRestaurante(string nombre) => new()
    {
        Id = Guid.NewGuid(),
        Nombre = nombre,
        Direccion = "Dirección",
        PlaceId = $"place-{nombre}",
        GustosQueSirve = new List<Gusto> { new() { Nombre = "Pizza" } },
        RestriccionesQueRespeta = new List<Restriccion>()
    };
}
