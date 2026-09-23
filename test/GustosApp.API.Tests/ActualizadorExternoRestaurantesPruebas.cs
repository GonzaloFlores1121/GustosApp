using GustosApp.Application.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Infraestructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GustosApp.API.Tests;

public sealed class ActualizadorExternoRestaurantesPruebas
{
    [Fact]
    public async Task ActualizarPendientes_RespetaAntiguedadPlaceIdYTamanoDeLote()
    {
        await using var contexto = Infraestructura.DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var pendiente = CrearRestaurante("pendiente", Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var segundoPendiente = CrearRestaurante("segundo", Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var reciente = CrearRestaurante("reciente");
        reciente.RegistrarActualizacionExternaExitosa(DateTime.UtcNow);
        var sinPlaceId = CrearRestaurante(string.Empty);
        contexto.Restaurantes.AddRange(pendiente, segundoPendiente, reciente, sinPlaceId);
        await contexto.SaveChangesAsync();

        var servicio = new Mock<IServicioRestaurantes>();
        servicio.Setup(s => s.ObtenerResenasDesdeGooglePlaces("pendiente", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendiente);
        var options = new Mock<IOptionsSnapshot<ActualizacionExternaRestaurantesOptions>>();
        options.SetupGet(o => o.Value).Returns(new ActualizacionExternaRestaurantesOptions
        {
            Habilitada = true,
            IntervaloMinutos = 60,
            TamanoLote = 1
        });
        var actualizador = new ActualizadorExternoRestaurantes(
            contexto,
            servicio.Object,
            options.Object,
            NullLogger<ActualizadorExternoRestaurantes>.Instance);

        var procesados = await actualizador.ActualizarPendientesAsync(CancellationToken.None);

        procesados.Should().Be(1);
        servicio.Verify(s => s.ObtenerResenasDesdeGooglePlaces(
            "pendiente",
            It.IsAny<CancellationToken>()), Times.Once);
        servicio.Verify(s => s.ObtenerResenasDesdeGooglePlaces(
            "reciente",
            It.IsAny<CancellationToken>()), Times.Never);
        servicio.Verify(s => s.ObtenerResenasDesdeGooglePlaces(
            It.Is<string>(id => string.IsNullOrWhiteSpace(id)),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Restaurante CrearRestaurante(string placeId, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        PlaceId = placeId,
        Nombre = placeId,
        NombreNormalizado = placeId,
        Direccion = "Dirección"
    };
}
