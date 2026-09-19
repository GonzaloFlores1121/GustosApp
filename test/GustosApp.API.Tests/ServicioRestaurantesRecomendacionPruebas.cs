using FluentAssertions;
using GustosApp.API.Tests.Infraestructura;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Infraestructure.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace GustosApp.API.Tests.Servicios
{
    public class ServicioRestaurantesRecomendacionPruebas
    {
        [Fact]
        public async Task BuscarAsync_IncluyeCandidatosSinCoincidenciaExactaParaElFallback()
        {
            await using var contexto = DbContextEnMemoria.Crear(
                nameof(BuscarAsync_IncluyeCandidatosSinCoincidenciaExactaParaElFallback));

            var gustoPizza = contexto.Gustos.Single(gusto => gusto.Nombre == "Pizza");
            var gustoSushi = contexto.Gustos.Single(gusto => gusto.Nombre == "Sushi");

            contexto.Restaurantes.AddRange(
                CrearRestaurante("Pizzería", gustoPizza),
                CrearRestaurante("Restaurante de sushi", gustoSushi));
            await contexto.SaveChangesAsync();

            var servicio = new ServicioRestaurantes(
                contexto,
                new ConfigurationBuilder().Build(),
                new HttpClient(),
                Mock.Of<IRestauranteRepository>());

            var resultado = await servicio.BuscarAsync(
                rating: 0,
                lat: null,
                lng: null,
                radioMetros: null,
                gustos: new List<string> { "Pizza" },
                restricciones: new List<string>());

            resultado.Should().HaveCount(2);
            resultado.Should().Contain(restaurante => restaurante.Nombre == "Restaurante de sushi");
            resultado.Single(restaurante => restaurante.Nombre == "Pizzería")
                .GustosQueSirve.Single()
                .Tags.Should().Contain(tag => tag.Nombre == "Gluten");
        }

        private static Restaurante CrearRestaurante(string nombre, Gusto gusto)
        {
            return new Restaurante
            {
                Id = Guid.NewGuid(),
                Nombre = nombre,
                NombreNormalizado = nombre.ToLowerInvariant(),
                Direccion = "Dirección de prueba",
                PlaceId = Guid.NewGuid().ToString(),
                Rating = 4,
                GustosQueSirve = new List<Gusto> { gusto }
            };
        }
    }
}
