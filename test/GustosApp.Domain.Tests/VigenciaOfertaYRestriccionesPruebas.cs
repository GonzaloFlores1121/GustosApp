using GustosApp.Domain.Common;
using GustosApp.Domain.Model;

namespace GustosApp.Domain.Tests;

public class VigenciaOfertaYRestriccionesPruebas
{
    [Theory]
    [InlineData(2024, 1, 31, 2024, 5, 31)]
    [InlineData(2023, 10, 31, 2024, 2, 29)]
    [InlineData(2024, 10, 31, 2025, 2, 28)]
    public void CuatroMesesCalendario_VencenAlAlcanzarFechaLimite(
        int anio, int mes, int dia, int anioLimite, int mesLimite, int diaLimite)
    {
        var restaurante = new Restaurante();
        var revision = new DateTime(anio, mes, dia, 12, 0, 0, DateTimeKind.Utc);
        restaurante.ConfirmarOfertaVigente(0, revision);
        var limite = new DateTime(anioLimite, mesLimite, diaLimite, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(EstadoVigenciaInformacion.Vigente,
            restaurante.ObtenerEstadoOferta(limite.AddTicks(-1)));
        Assert.Equal(EstadoVigenciaInformacion.Desactualizada,
            restaurante.ObtenerEstadoOferta(limite));
    }

    [Theory]
    [InlineData(2024, 1, 31, 2024, 5, 31)]
    [InlineData(2023, 10, 31, 2024, 2, 29)]
    [InlineData(2024, 10, 31, 2025, 2, 28)]
    public void Restricciones_CuatroMesesCalendario_VencenAlAlcanzarFechaLimite(
        int anio, int mes, int dia, int anioLimite, int mesLimite, int diaLimite)
    {
        var restaurante = new Restaurante();
        var revision = new DateTime(anio, mes, dia, 12, 0, 0, DateTimeKind.Utc);
        restaurante.ConfirmarRestriccionesDeclaradas(0, revision);
        var limite = new DateTime(anioLimite, mesLimite, diaLimite, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(EstadoVigenciaInformacion.Vigente,
            restaurante.ObtenerEstadoRestricciones(limite.AddTicks(-1)));
        Assert.Equal(EstadoVigenciaInformacion.Desactualizada,
            restaurante.ObtenerEstadoRestricciones(limite));
    }

    [Fact]
    public void DeclaracionIndividual_ConservaSuFechaYVenceAunqueSeAgregueOtra()
    {
        var original = new Restriccion { Id = Guid.NewGuid(), Nombre = "Sin gluten" };
        var nueva = new Restriccion { Id = Guid.NewGuid(), Nombre = "Sin lactosa" };
        var restaurante = new Restaurante { RestriccionesQueRespeta = [original] };
        var revision = new DateTime(2026, 1, 31, 12, 0, 0, DateTimeKind.Utc);
        restaurante.ConfirmarRestriccionesDeclaradas(0, revision);
        restaurante.RestriccionesQueRespeta.Add(nueva);
        restaurante.RegistrarCambioRestricciones([original.Id], revision.AddMonths(2));
        var limite = revision.AddMonths(4);

        Assert.Equal(EstadoVigenciaInformacion.Vigente,
            restaurante.ObtenerEstadoDeclaracion(original.Id, limite.AddTicks(-1)));
        Assert.Equal(EstadoVigenciaInformacion.Desactualizada,
            restaurante.ObtenerEstadoDeclaracion(original.Id, limite));
        Assert.Equal(EstadoVigenciaInformacion.Vigente,
            restaurante.ObtenerEstadoDeclaracion(nueva.Id, limite));
    }

    [Fact]
    public void GustoIndividual_ConservaSuFechaAunqueSeAgregueOtro()
    {
        var original = new Gusto { Id = Guid.NewGuid(), Nombre = "Pizza" };
        var nuevo = new Gusto { Id = Guid.NewGuid(), Nombre = "Empanadas" };
        var restaurante = new Restaurante { GustosQueSirve = [original] };
        var revision = new DateTime(2026, 1, 31, 12, 0, 0, DateTimeKind.Utc);
        restaurante.ConfirmarOfertaVigente(0, revision);
        restaurante.GustosQueSirve.Add(nuevo);
        restaurante.RegistrarCambioOferta([original.Id], revision.AddMonths(2));

        Assert.Equal(revision, restaurante.ObtenerFechaRevisionGusto(original.Id));
        Assert.Equal(revision.AddMonths(2), restaurante.ObtenerFechaRevisionGusto(nuevo.Id));
        Assert.Equal(EstadoVigenciaInformacion.SinFecha,
            restaurante.ObtenerEstadoOferta(revision.AddMonths(2)));
    }

    [Fact]
    public void Secciones_SeConfirmanPorSeparadoYSinFechaNoSeInventaVigencia()
    {
        var restaurante = new Restaurante();
        var fecha = new DateTime(2026, 1, 31, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(EstadoVigenciaInformacion.SinFecha,
            restaurante.ObtenerEstadoOferta(fecha));
        Assert.Equal(EstadoVigenciaInformacion.SinFecha,
            restaurante.ObtenerEstadoRestricciones(fecha));

        restaurante.ConfirmarOfertaVigente(0, fecha);
        Assert.Equal(EstadoVigenciaInformacion.Vigente,
            restaurante.ObtenerEstadoOferta(fecha));
        Assert.Equal(EstadoVigenciaInformacion.SinFecha,
            restaurante.ObtenerEstadoRestricciones(fecha));

        restaurante.ConfirmarRestriccionesDeclaradas(0, fecha.AddDays(1));
        Assert.Equal(fecha, restaurante.FechaRevisionOfertaUtc);
        Assert.Equal(fecha.AddDays(1), restaurante.FechaRevisionRestriccionesUtc);
    }

    [Fact]
    public void AgregarDeclaracion_NoExtiendeFechaDeLaAnteriorNiConfirmaTodaLaSeccion()
    {
        var vieja = new Restriccion { Id = Guid.NewGuid(), Nombre = "Sin gluten" };
        var nueva = new Restriccion { Id = Guid.NewGuid(), Nombre = "Sin lactosa" };
        var restaurante = new Restaurante { RestriccionesQueRespeta = [vieja] };
        var revision = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        restaurante.ConfirmarRestriccionesDeclaradas(0, revision);
        restaurante.RestriccionesQueRespeta.Add(nueva);
        restaurante.RegistrarCambioRestricciones([vieja.Id], revision.AddMonths(5));

        Assert.Equal(EstadoVigenciaInformacion.Desactualizada,
            restaurante.ObtenerEstadoDeclaracion(vieja.Id, revision.AddMonths(5)));
        Assert.Equal(EstadoVigenciaInformacion.Vigente,
            restaurante.ObtenerEstadoDeclaracion(nueva.Id, revision.AddMonths(5)));
        Assert.Null(restaurante.FechaRevisionRestriccionesUtc);
    }

    [Fact]
    public void CambioDeMenuInvalidaRevisionDeOfertaSinBorrarGustos()
    {
        var gusto = new Gusto { Id = Guid.NewGuid(), Nombre = "Pizza" };
        var restaurante = new Restaurante { GustosQueSirve = [gusto] };
        var fecha = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        restaurante.ConfirmarOfertaVigente(0, fecha);

        restaurante.RegistrarCambioMenuPublicado();

        Assert.Equal(EstadoVigenciaInformacion.SinFecha,
            restaurante.ObtenerEstadoOferta(fecha.AddDays(1)));
        Assert.Contains(gusto, restaurante.GustosQueSirve);
        Assert.Equal(fecha, restaurante.ObtenerFechaRevisionGusto(gusto.Id));
    }

    [Fact]
    public void ConfirmacionConVersionAntigua_NoModificaFecha()
    {
        var restaurante = new Restaurante();
        restaurante.RegistrarCambioMenuPublicado();

        Assert.Throws<InvalidOperationException>(() =>
            restaurante.ConfirmarOfertaVigente(0, DateTime.UtcNow));
        Assert.Null(restaurante.FechaRevisionOfertaUtc);
    }
}
