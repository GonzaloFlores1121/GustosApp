using GustosApp.Application.Interfaces;
using GustosApp.Application.Model;
using GustosApp.Application.UseCases.GrupoUseCases;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;
using Xunit;

namespace GustosApp.Application.Tests;

public class BuscarRestaurantesRecomendadosGrupoUseCaseTests
{
    [Fact]
    public async Task InactivoNoEntraAlRankingDelGrupoAunqueElProveedorLoDevuelva()
    {
        var preferencias = new UsuarioPreferencias { Gustos = ["Pizza"] };
        _preferencias.Setup(s => s.HandleAsync("usuario-1", It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(preferencias);
        var inactivo = new Restaurante();
        inactivo.CambiarDisponibilidad(true, 0, "Revisión", DateTime.UtcNow);
        _servicioRestaurantes.Setup(s => s.BuscarAsync(0, null, null, null, preferencias.Gustos, preferencias.Restricciones)).ReturnsAsync([inactivo]);
        var resultado = await CrearCasoDeUso().BuscarAsync("usuario-1", Guid.NewGuid(), null, null, null, 1);
        Assert.Equal(EstadoBusquedaRestaurantesGrupo.SinOpciones, resultado.Estado);
        Assert.Empty(resultado.Restaurantes);
        _recomendador.VerifyNoOtherCalls();
    }
    private readonly Mock<IConstruirPreferenciasGrupoUseCase> _preferencias = new();
    private readonly Mock<IServicioRestaurantes> _servicioRestaurantes = new();
    private readonly Mock<IRecomendadorRestaurantes> _recomendador = new();

    [Fact]
    public async Task BuscarAsync_SinGustos_DistinguePreferenciasInsuficientes()
    {
        var sut = CrearCasoDeUso();
        _preferencias
            .Setup(servicio => servicio.HandleAsync(
                "usuario-1", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UsuarioPreferencias());

        var resultado = await sut.BuscarAsync(
            "usuario-1", Guid.NewGuid(), null, null, null, 10);

        Assert.Equal(EstadoBusquedaRestaurantesGrupo.SinPreferencias, resultado.Estado);
        Assert.Empty(resultado.Restaurantes);
        _servicioRestaurantes.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BuscarAsync_UsaPreferenciasCompletasYDevuelveSoloRecomendados()
    {
        var grupoId = Guid.NewGuid();
        var preferencias = new UsuarioPreferencias
        {
            Gustos = new List<string> { "Pizza" },
            Restricciones = new List<string> { "Sin gluten" },
            CondicionesMedicas = new List<string> { "Diabetes" }
        };
        var candidato = new Restaurante { Id = Guid.NewGuid() };
        var recomendado = new Restaurante { Id = Guid.NewGuid() };
        var sut = CrearCasoDeUso();
        _preferencias
            .Setup(servicio => servicio.HandleAsync(
                "usuario-1", grupoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferencias);
        _servicioRestaurantes
            .Setup(servicio => servicio.BuscarAsync(
                0, -34.6, -58.4, 1200, preferencias.Gustos, preferencias.Restricciones))
            .ReturnsAsync(new List<Restaurante> { candidato, recomendado });
        _recomendador
            .Setup(recomendador => recomendador.Handle(
                preferencias,
                It.IsAny<List<Restaurante>>(),
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Restaurante> { recomendado });

        var resultado = await sut.BuscarAsync(
            "usuario-1", grupoId, -34.6, -58.4, 1200, 10);

        Assert.Equal(EstadoBusquedaRestaurantesGrupo.Encontrados, resultado.Estado);
        Assert.Single(resultado.Restaurantes);
        Assert.Equal(recomendado.Id, resultado.Restaurantes[0].Id);
    }

    [Fact]
    public async Task BuscarAsync_SinCandidatos_DistingueAusenciaDeOpciones()
    {
        var grupoId = Guid.NewGuid();
        var preferencias = new UsuarioPreferencias
        {
            Gustos = new List<string> { "Pizza" }
        };
        var sut = CrearCasoDeUso();
        _preferencias
            .Setup(servicio => servicio.HandleAsync(
                "usuario-1", grupoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferencias);
        _servicioRestaurantes
            .Setup(servicio => servicio.BuscarAsync(
                0, null, null, null, preferencias.Gustos, preferencias.Restricciones))
            .ReturnsAsync(new List<Restaurante>());

        var resultado = await sut.BuscarAsync(
            "usuario-1", grupoId, null, null, null, 10);

        Assert.Equal(EstadoBusquedaRestaurantesGrupo.SinOpciones, resultado.Estado);
        Assert.Empty(resultado.Restaurantes);
        _recomendador.VerifyNoOtherCalls();
    }

    private BuscarRestaurantesRecomendadosGrupoUseCase CrearCasoDeUso() => new(
        _preferencias.Object,
        _servicioRestaurantes.Object,
        _recomendador.Object);
}
