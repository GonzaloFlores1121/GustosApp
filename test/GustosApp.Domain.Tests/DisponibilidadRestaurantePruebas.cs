using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;

namespace GustosApp.Domain.Tests;

public sealed class DisponibilidadRestaurantePruebas
{
    [Fact]
    public void DesactivarYReactivarConservaFichaYRepetirNoCambiaVersion()
    {
        var restaurante = new Restaurante { Id = Guid.NewGuid(), DuenoId = Guid.NewGuid(), PlaceId = "mismo-local" };
        var gusto = new Gusto { Id = Guid.NewGuid(), Nombre = "Pizza" };
        restaurante.GustosQueSirve.Add(gusto);
        var fecha = DateTime.UtcNow;
        Assert.False(restaurante.Desactivado);
        Assert.True(restaurante.CambiarDisponibilidad(true, 0, "  Revisión  ", fecha));
        Assert.Equal("Revisión", restaurante.MotivoDisponibilidad);
        Assert.Equal(fecha, restaurante.FechaCambioDisponibilidadUtc);
        Assert.False(restaurante.CambiarDisponibilidad(true, 0, "Reintento", fecha));
        Assert.Equal(1, restaurante.VersionDisponibilidad);
        Assert.Throws<InvalidOperationException>(() => restaurante.CambiarDisponibilidad(false, 0, "Reactivar", fecha));
        Assert.True(restaurante.CambiarDisponibilidad(false, 1, "Resuelto", fecha));
        Assert.Equal(2, restaurante.VersionDisponibilidad);
        Assert.Equal("mismo-local", restaurante.PlaceId);
        Assert.NotNull(restaurante.DuenoId);
        Assert.Same(gusto, Assert.Single(restaurante.GustosQueSirve));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ExigeMotivoInclusoEnReintento(string motivo)
        => Assert.Throws<ArgumentException>(() => new Restaurante().CambiarDisponibilidad(false, 0, motivo, DateTime.UtcNow));

    [Fact]
    public void RechazoSeArchivaYRestauraSinCambiarDecision()
    {
        var solicitud = new SolicitudRestaurante { Estado = EstadoSolicitudRestaurante.Rechazada, MotivoRechazo = "Falta documentación" };
        Assert.True(solicitud.CambiarArchivo(true, 0, DateTime.UtcNow));
        Assert.False(solicitud.CambiarArchivo(true, 0, DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() => solicitud.CambiarArchivo(false, 0, DateTime.UtcNow));
        Assert.True(solicitud.CambiarArchivo(false, 1, DateTime.UtcNow));
        Assert.Equal("Falta documentación", solicitud.MotivoRechazo);
        Assert.Equal(EstadoSolicitudRestaurante.Rechazada, solicitud.Estado);
    }

    [Theory]
    [InlineData(EstadoSolicitudRestaurante.Pendiente)]
    [InlineData(EstadoSolicitudRestaurante.Aprobada)]
    public void NoArchivaNiRestauraOtrasDecisiones(EstadoSolicitudRestaurante estado)
    {
        var solicitud = new SolicitudRestaurante { Estado = estado };
        Assert.Throws<InvalidOperationException>(() => solicitud.CambiarArchivo(true, 0, DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() => solicitud.CambiarArchivo(false, 0, DateTime.UtcNow));
    }
}
