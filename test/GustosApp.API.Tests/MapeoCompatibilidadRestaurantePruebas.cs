using AutoMapper;
using FluentAssertions;
using GustosApp.API.DTO;
using GustosApp.API.Mapping.RestaurantesMapper;
using GustosApp.Domain.Common;
using GustosApp.Domain.Model;
using Microsoft.Extensions.Logging.Abstractions;

namespace GustosApp.API.Tests
{
    public class MapeoCompatibilidadRestaurantePruebas
    {
        [Theory]
        [InlineData(NivelCompatibilidadRestaurante.Desconocida, "No hay datos suficientes")]
        [InlineData(NivelCompatibilidadRestaurante.Estimada, "orientativa")]
        public void Mapear_IncluyeNivelYAdvertenciaDeCompatibilidad(
            NivelCompatibilidadRestaurante nivel,
            string fragmentoAdvertencia)
        {
            var configuracion = new MapperConfiguration(
                expresion => expresion.AddProfile<RestauranteProfile>(),
                NullLoggerFactory.Instance);
            var mapper = configuracion.CreateMapper();
            var restaurante = new Restaurante
            {
                NivelCompatibilidad = nivel
            };

            var resultado = mapper.Map<RestauranteDTO>(restaurante);

            resultado.NivelCompatibilidad.Should().Be(nivel.ToString());
            resultado.AdvertenciaCompatibilidad.Should().Contain(fragmentoAdvertencia);
        }

        [Fact]
        public void Mapear_DatosConfirmadosPorRestaurante_IncluyeTrazabilidadYAdvertenciaOrientativa()
        {
            var configuracion = CrearConfiguracion();
            var mapper = configuracion.CreateMapper();
            var fecha = DateTime.UtcNow.AddDays(-10);
            var restaurante = new Restaurante
            {
                NivelCompatibilidad = NivelCompatibilidadRestaurante.Estimada
            };
            restaurante.RegistrarVerificacionDatosCompatibilidad(
                OrigenDatosCompatibilidadRestaurante.Restaurante,
                fecha);

            var resultado = mapper.Map<RestauranteDTO>(restaurante);

            resultado.OrigenDatosCompatibilidad.Should().Be("Restaurante");
            resultado.EstadoDatosCompatibilidad.Should().Be("Verificado");
            resultado.AdvertenciaCompatibilidad.Should().Contain("orientativa");
        }

        [Fact]
        public void Mapear_OfertaRevisadaHaceCuatroMeses_NoGeneraAdvertenciaTemporal()
        {
            var configuracion = CrearConfiguracion();
            var mapper = configuracion.CreateMapper();
            var restaurante = new Restaurante
            {
                NivelCompatibilidad = NivelCompatibilidadRestaurante.Estimada
            };
            restaurante.ConfirmarOfertaVigente(0, DateTime.UtcNow.AddMonths(-4).AddDays(-1));

            var resultado = mapper.Map<RestauranteDTO>(restaurante);

            resultado.AdvertenciaCompatibilidad.Should().NotContain("recientemente").And.NotContain("fecha").And.NotContain("pendientes");
        }

        private static MapperConfiguration CrearConfiguracion()
        {
            return new MapperConfiguration(
                expresion => expresion.AddProfile<RestauranteProfile>(),
                NullLoggerFactory.Instance);
        }
    }
}
