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
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActualizarPendientes_NoConsultaFichasConPropietario(bool usaDuenoId)
    {
        await using var contexto = Infraestructura.DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var restaurante = CrearRestaurante("reclamado");
        restaurante.DuenoId = usaDuenoId ? Guid.NewGuid() : null;
        restaurante.PropietarioUid = usaDuenoId ? "" : "propietario-prueba";
        contexto.Add(restaurante);
        await contexto.SaveChangesAsync();
        var servicio = new Mock<IServicioRestaurantes>();
        servicio.Setup(s => s.ObtenerResenasDesdeGooglePlaces(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(restaurante);
        var options = new Mock<IOptionsSnapshot<ActualizacionExternaRestaurantesOptions>>();
        options.SetupGet(o => o.Value).Returns(new ActualizacionExternaRestaurantesOptions { Habilitada = true });
        var actualizador = new ActualizadorExternoRestaurantes(contexto, servicio.Object, options.Object, NullLogger<ActualizadorExternoRestaurantes>.Instance);
        await actualizador.ActualizarPendientesAsync(default);
        servicio.VerifyNoOtherCalls();
    }

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
        servicio.Setup(s => s.ActualizarImportadoDesdeGooglePlacesAsync(pendiente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoActualizacionExternaRestaurante.Exitosa);
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

        procesados.Should().Be(new ResultadoLoteActualizacionExterna(1, 1, 0, 0));
        servicio.Verify(s => s.ActualizarImportadoDesdeGooglePlacesAsync(
            pendiente.Id,
            It.IsAny<CancellationToken>()), Times.Once);
        servicio.Verify(s => s.ActualizarImportadoDesdeGooglePlacesAsync(
            reciente.Id,
            It.IsAny<CancellationToken>()), Times.Never);
        servicio.Verify(s => s.ActualizarImportadoDesdeGooglePlacesAsync(
            sinPlaceId.Id,
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deshabilitada_NoConsultaProveedorNiModificaFechas()
    {
        await using var db = Infraestructura.DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var pendiente = CrearRestaurante("pendiente");
        db.Add(pendiente);
        await db.SaveChangesAsync();
        var servicio = new Mock<IServicioRestaurantes>(MockBehavior.Strict);
        var options = new Mock<IOptionsSnapshot<ActualizacionExternaRestaurantesOptions>>();
        options.SetupGet(o => o.Value).Returns(new ActualizacionExternaRestaurantesOptions { Habilitada = false });
        var resultado = await new ActualizadorExternoRestaurantes(db, servicio.Object, options.Object,
            NullLogger<ActualizadorExternoRestaurantes>.Instance).ActualizarPendientesAsync(default);
        resultado.Should().Be(new ResultadoLoteActualizacionExterna(0, 0, 0, 0));
        pendiente.FechaUltimoIntentoActualizacionExternaUtc.Should().BeNull();
        servicio.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Lote_SeparaExitosErroresYOmitidos_YContinuaTrasExcepcion()
    {
        await using var db = Infraestructura.DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var fichas = Enumerable.Range(1, 4).Select(i => CrearRestaurante($"place-{i}", new Guid(i, 0, 0, new byte[8]))).ToArray();
        db.AddRange(fichas);
        await db.SaveChangesAsync();
        var servicio = new Mock<IServicioRestaurantes>();
        servicio.Setup(s => s.ActualizarImportadoDesdeGooglePlacesAsync(fichas[0].Id, It.IsAny<CancellationToken>())).ReturnsAsync(ResultadoActualizacionExternaRestaurante.Fallida);
        servicio.Setup(s => s.ActualizarImportadoDesdeGooglePlacesAsync(fichas[1].Id, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Fallo controlado"));
        servicio.Setup(s => s.ActualizarImportadoDesdeGooglePlacesAsync(fichas[2].Id, It.IsAny<CancellationToken>())).ReturnsAsync(ResultadoActualizacionExternaRestaurante.Omitida);
        servicio.Setup(s => s.ActualizarImportadoDesdeGooglePlacesAsync(fichas[3].Id, It.IsAny<CancellationToken>())).ReturnsAsync(ResultadoActualizacionExternaRestaurante.Exitosa);
        var options = new Mock<IOptionsSnapshot<ActualizacionExternaRestaurantesOptions>>();
        options.SetupGet(o => o.Value).Returns(new ActualizacionExternaRestaurantesOptions { Habilitada = true });
        var resultado = await new ActualizadorExternoRestaurantes(db, servicio.Object, options.Object,
            NullLogger<ActualizadorExternoRestaurantes>.Instance).ActualizarPendientesAsync(default);
        resultado.Should().Be(new ResultadoLoteActualizacionExterna(4, 1, 2, 1));
        servicio.Verify(s => s.ActualizarImportadoDesdeGooglePlacesAsync(fichas[3].Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ErrorRegistrado_RespetaIntervaloDeReintentoSinConfundirloConVigencia()
    {
        await using var db = Infraestructura.DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        var reciente = CrearRestaurante("fallo-reciente");
        reciente.RegistrarErrorActualizacionExterna(DateTime.UtcNow.AddMinutes(-10), "HTTP503");
        var antiguo = CrearRestaurante("fallo-antiguo");
        antiguo.RegistrarErrorActualizacionExterna(DateTime.UtcNow.AddMinutes(-90), "HTTP503");
        db.AddRange(reciente, antiguo);
        await db.SaveChangesAsync();
        var servicio = new Mock<IServicioRestaurantes>();
        servicio.Setup(s => s.ActualizarImportadoDesdeGooglePlacesAsync(antiguo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ResultadoActualizacionExternaRestaurante.Exitosa);
        var options = new Mock<IOptionsSnapshot<ActualizacionExternaRestaurantesOptions>>();
        options.SetupGet(o => o.Value).Returns(new ActualizacionExternaRestaurantesOptions { Habilitada = true, IntervaloMinutos = 60 });
        await new ActualizadorExternoRestaurantes(db, servicio.Object, options.Object,
            NullLogger<ActualizadorExternoRestaurantes>.Instance).ActualizarPendientesAsync(default);
        servicio.Verify(s => s.ActualizarImportadoDesdeGooglePlacesAsync(reciente.Id, It.IsAny<CancellationToken>()), Times.Never);
        servicio.Verify(s => s.ActualizarImportadoDesdeGooglePlacesAsync(antiguo.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancelacion_NoSeCuentaComoFalloNiProcesaOtraFicha()
    {
        await using var db = Infraestructura.DbContextEnMemoria.Crear(Guid.NewGuid().ToString());
        db.AddRange(CrearRestaurante("primero"), CrearRestaurante("segundo"));
        await db.SaveChangesAsync();
        using var cts = new CancellationTokenSource();
        var servicio = new Mock<IServicioRestaurantes>();
        servicio.Setup(s => s.ActualizarImportadoDesdeGooglePlacesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, CancellationToken ct) => { cts.Cancel(); return Task.FromCanceled<ResultadoActualizacionExternaRestaurante>(ct); });
        var options = new Mock<IOptionsSnapshot<ActualizacionExternaRestaurantesOptions>>();
        options.SetupGet(o => o.Value).Returns(new ActualizacionExternaRestaurantesOptions { Habilitada = true });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ActualizadorExternoRestaurantes(db, servicio.Object, options.Object,
            NullLogger<ActualizadorExternoRestaurantes>.Instance).ActualizarPendientesAsync(cts.Token));
        servicio.Verify(s => s.ActualizarImportadoDesdeGooglePlacesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
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
