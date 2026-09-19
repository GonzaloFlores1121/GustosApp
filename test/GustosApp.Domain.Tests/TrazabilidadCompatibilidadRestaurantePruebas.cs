using GustosApp.Domain.Common;
using GustosApp.Domain.Model;
using Xunit;

namespace GustosApp.Domain.Tests
{
    public class TrazabilidadCompatibilidadRestaurantePruebas
    {
        [Fact]
        public void RegistrarDatosEstimados_GuardaOrigenYFechaSinMarcarVerificacion()
        {
            var restaurante = new Restaurante();
            var fecha = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);

            restaurante.RegistrarDatosCompatibilidadEstimados(
                OrigenDatosCompatibilidadRestaurante.Estimacion,
                fecha);

            Assert.Equal(OrigenDatosCompatibilidadRestaurante.Estimacion, restaurante.OrigenDatosCompatibilidad);
            Assert.Equal(EstadoDatosCompatibilidadRestaurante.Estimado, restaurante.EstadoDatosCompatibilidad);
            Assert.Equal(fecha, restaurante.FechaObtencionDatosCompatibilidadUtc);
            Assert.Null(restaurante.FechaUltimaVerificacionDatosCompatibilidadUtc);
        }

        [Fact]
        public void RegistrarVerificacion_GuardaRestauranteComoOrigenYFechaDeVerificacion()
        {
            var restaurante = new Restaurante();
            var fecha = new DateTime(2026, 2, 15, 10, 30, 0, DateTimeKind.Utc);

            restaurante.RegistrarVerificacionDatosCompatibilidad(
                OrigenDatosCompatibilidadRestaurante.Restaurante,
                fecha);

            Assert.Equal(OrigenDatosCompatibilidadRestaurante.Restaurante, restaurante.OrigenDatosCompatibilidad);
            Assert.Equal(EstadoDatosCompatibilidadRestaurante.Verificado, restaurante.EstadoDatosCompatibilidad);
            Assert.Equal(fecha, restaurante.FechaObtencionDatosCompatibilidadUtc);
            Assert.Equal(fecha, restaurante.FechaUltimaVerificacionDatosCompatibilidadUtc);
        }

        [Theory]
        [InlineData(180, EstadoDatosCompatibilidadRestaurante.Verificado)]
        [InlineData(181, EstadoDatosCompatibilidadRestaurante.Desactualizado)]
        public void ObtenerEstadoActual_CambiaAVencidoDespuesDeLaVigencia(
            int diasTranscurridos,
            EstadoDatosCompatibilidadRestaurante estadoEsperado)
        {
            var restaurante = new Restaurante();
            var fechaVerificacion = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            restaurante.RegistrarVerificacionDatosCompatibilidad(
                OrigenDatosCompatibilidadRestaurante.Restaurante,
                fechaVerificacion);

            var estado = restaurante.ObtenerEstadoActualDatosCompatibilidad(
                fechaVerificacion.AddDays(diasTranscurridos));

            Assert.Equal(estadoEsperado, estado);
        }
    }
}
