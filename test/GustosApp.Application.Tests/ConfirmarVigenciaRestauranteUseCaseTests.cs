using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;

namespace GustosApp.Application.Tests;

public class ConfirmarVigenciaRestauranteUseCaseTests
{
    [Fact]
    public async Task PropietarioConfirmaOferta_SinModificarRestricciones()
    {
        var propietarioId = Guid.NewGuid();
        var restaurante = new Restaurante
        {
            Id = Guid.NewGuid(), DuenoId = propietarioId,
            GustosQueSirve = [new Gusto { Id = Guid.NewGuid(), Nombre = "Pizza" }]
        };
        var repositorio = CrearRepositorio(restaurante);
        var useCase = new ConfirmarVigenciaRestauranteUseCase(repositorio.Object, TimeProvider.System);

        await useCase.ConfirmarOfertaAsync(restaurante.Id, 0, propietarioId, false);

        Assert.Equal(EstadoVigenciaInformacion.Vigente, restaurante.ObtenerEstadoOferta(DateTime.UtcNow));
        Assert.Null(restaurante.FechaRevisionRestriccionesUtc);
        Assert.Equal(1, restaurante.VersionOferta);
        repositorio.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdministradorNoPuedeConfirmarFichaConPropietario()
    {
        var restaurante = new Restaurante { Id = Guid.NewGuid(), DuenoId = Guid.NewGuid() };
        var repositorio = CrearRepositorio(restaurante);
        var useCase = new ConfirmarVigenciaRestauranteUseCase(repositorio.Object, TimeProvider.System);

        await Assert.ThrowsAsync<AccesoProhibidoException>(() =>
            useCase.ConfirmarRestriccionesAsync(restaurante.Id, 0, null, true));
        repositorio.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PropietarioAjenoNoPuedeConfirmar()
    {
        var restaurante = new Restaurante { Id = Guid.NewGuid(), DuenoId = Guid.NewGuid() };
        var repositorio = CrearRepositorio(restaurante);
        var useCase = new ConfirmarVigenciaRestauranteUseCase(repositorio.Object, TimeProvider.System);

        await Assert.ThrowsAsync<AccesoProhibidoException>(() =>
            useCase.ConfirmarRestriccionesAsync(restaurante.Id, 0, Guid.NewGuid(), false));
    }

    [Fact]
    public async Task ConfirmacionConVersionObsoleta_NoGuardaNiRenueva()
    {
        var restaurante = new Restaurante { Id = Guid.NewGuid() };
        restaurante.RegistrarCambioMenuPublicado();
        var repositorio = CrearRepositorio(restaurante);
        var useCase = new ConfirmarVigenciaRestauranteUseCase(repositorio.Object, TimeProvider.System);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            useCase.ConfirmarRestriccionesAsync(restaurante.Id, -1, null, true));
        Assert.Null(restaurante.FechaRevisionRestriccionesUtc);
        repositorio.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdministradorPuedeConfirmarDeclaracionesDeImportado()
    {
        var restaurante = new Restaurante
        {
            Id = Guid.NewGuid(),
            RestriccionesQueRespeta = [new Restriccion { Id = Guid.NewGuid(), Nombre = "Sin gluten" }]
        };
        var repositorio = CrearRepositorio(restaurante);
        var useCase = new ConfirmarVigenciaRestauranteUseCase(repositorio.Object, TimeProvider.System);

        await useCase.ConfirmarRestriccionesAsync(restaurante.Id, 0, null, true);

        Assert.Equal(EstadoVigenciaInformacion.Vigente,
            restaurante.ObtenerEstadoDeclaracion(restaurante.RestriccionesQueRespeta.Single().Id, DateTime.UtcNow));
        Assert.Null(restaurante.FechaRevisionOfertaUtc);
    }

    private static Mock<IRestauranteRepository> CrearRepositorio(Restaurante restaurante)
    {
        var repositorio = new Mock<IRestauranteRepository>();
        repositorio.Setup(r => r.GetByIdAsync(restaurante.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurante);
        return repositorio;
    }
}
