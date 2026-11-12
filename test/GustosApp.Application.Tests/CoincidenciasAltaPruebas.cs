using FluentAssertions;
using GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;

namespace GustosApp.Application.Tests;

public sealed class CoincidenciasAltaPruebas
{
    [Fact]
    public async Task EncuentraMismaDireccionConOtroNombreYDescartaSucursalesYLocalesDistintos()
    {
        var repo = new Mock<IRestauranteRepository>();
        var edificio = new LocalCercano(Guid.NewGuid(), "Otra marca", "Avenida Córdoba 1234, Buenos Aires", -34.6, -58.401, true);
        var parecido = new LocalCercano(Guid.NewGuid(), "La Esquina Norte", "Córdoba 1236", -34.6, -58.4003, false);
        var casiMismoPunto = new LocalCercano(Guid.NewGuid(), "Panadería", "Local 7", -34.6, -58.40005, false);
        repo.Setup(r => r.BuscarLocalesEnVentanaAsync(-34.6, -58.4, It.IsAny<double>(), It.IsAny<double>(), default))
            .ReturnsAsync([
                casiMismoPunto,
                new(Guid.NewGuid(), "Heladería", "Córdoba 1270", -34.6, -58.4005, false),
                new(Guid.NewGuid(), "La Esquina Norte", "Córdoba 1234", -34.6, -58.43, false),
                parecido,
                edificio,
            ]);

        var resultado = await new BuscarCoincidenciasAltaUseCase(repo.Object)
            .HandleAsync("Restaurante La Esquina Norte", " AV.  CÓRDOBA, 1234 ", -34.6, -58.4, default);

        resultado.Candidatos.Select(c => c.Id).Should().Equal(edificio.Id, casiMismoPunto.Id, parecido.Id);
        resultado.Candidatos[0].Motivos.Should().Equal("Misma dirección");
        resultado.Candidatos[0].Administrado.Should().BeTrue();
        resultado.Candidatos[2].Motivos.Should().Contain("Nombre similar y ubicación cercana");
    }

    [Theory]
    [InlineData(null, 1d)]
    [InlineData(91d, 1d)]
    [InlineData(0d, 181d)]
    [InlineData(0d, 0d)]
    [InlineData(double.NaN, 1d)]
    public async Task UbicacionInvalida_NoConsultaRepositorio(double? latitud, double? longitud)
    {
        var repo = new Mock<IRestauranteRepository>(MockBehavior.Strict);
        var resultado = await new BuscarCoincidenciasAltaUseCase(repo.Object)
            .HandleAsync("Local", "Calle 123", latitud, longitud, default);

        resultado.UbicacionDisponible.Should().BeFalse();
        resultado.Candidatos.Should().BeEmpty();
    }

    [Fact]
    public async Task LimitaResultadosYExcluyeLaFichaYaAprobada()
    {
        var repo = new Mock<IRestauranteRepository>();
        var locales = Enumerable.Range(0, 8).Select(_ => new LocalCercano(Guid.NewGuid(), "Local", "Av. Sur 50", 90, 180, false)).ToList();
        repo.Setup(r => r.BuscarLocalesEnVentanaAsync(90, 180, It.IsAny<double>(), 180, default)).ReturnsAsync(locales);

        var resultado = await new BuscarCoincidenciasAltaUseCase(repo.Object)
            .HandleAsync(null, "Sur 50", 90, 180, default, locales[0].Id);

        resultado.Candidatos.Should().HaveCount(5).And.NotContain(c => c.Id == locales[0].Id);
    }
}
