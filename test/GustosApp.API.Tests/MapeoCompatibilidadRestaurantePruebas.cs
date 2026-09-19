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
        [InlineData(NivelCompatibilidadRestaurante.Estimada, "todavía no fueron verificados")]
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
    }
}
