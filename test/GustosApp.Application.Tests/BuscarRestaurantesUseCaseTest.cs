using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GustosApp.Application.Tests
{
    public class BuscarRestaurantesUseCaseTest
    {
        [Fact]
        public async Task HandleAsync_LlamaAlRepositorioConTextoCorrecto()
        {
            var restauranteMock = new Mock<IRestauranteRepository>();
            var casoDeUso = new BuscarRestaurantesUseCase(restauranteMock.Object);

            var textoBusqueda = "pi";

            restauranteMock
                .Setup(r => r.BuscarPorPrefijo(textoBusqueda, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Restaurante> { new Restaurante { Nombre = "Pizzeria Juan" }});

            var resultado = await casoDeUso.HandleAsync(textoBusqueda, CancellationToken.None);

            Assert.Single(resultado);
            Assert.Equal("Pizzeria Juan", resultado[0].Nombre);

            restauranteMock.Verify(r => r.BuscarPorPrefijo(textoBusqueda, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_SinResultados_RetornaListaVacia()
        {
            var repoMock = new Mock<IRestauranteRepository>();
            var useCase = new BuscarRestaurantesUseCase(repoMock.Object);

            repoMock.Setup(r => r.BuscarPorPrefijo("x", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Restaurante>());

            var result = await useCase.HandleAsync("x", CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task HandleAsync_NormalizaEspaciosAntesDeBuscar()
        {
            var repoMock = new Mock<IRestauranteRepository>();
            var useCase = new BuscarRestaurantesUseCase(repoMock.Object);
            repoMock.Setup(r => r.BuscarPorPrefijo("Las Leñas", It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            await useCase.HandleAsync("  Las Leñas  ", CancellationToken.None);

            repoMock.Verify(r => r.BuscarPorPrefijo("Las Leñas", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_TextoDemasiadoLargo_NoConsultaRepositorio()
        {
            var repoMock = new Mock<IRestauranteRepository>();
            var useCase = new BuscarRestaurantesUseCase(repoMock.Object);

            await Assert.ThrowsAsync<ArgumentException>(() => useCase.HandleAsync(
                new string('a', BuscarRestaurantesUseCase.LongitudMaximaBusqueda + 1), CancellationToken.None));

            repoMock.Verify(r => r.BuscarPorPrefijo(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }




    }
}
