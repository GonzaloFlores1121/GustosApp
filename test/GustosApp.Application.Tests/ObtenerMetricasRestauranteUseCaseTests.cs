using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Application.record;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Moq;
using Xunit;

namespace GustosApp.Application.Tests
{
    public class ObtenerMetricasRestauranteUseCaseTests
    {
        private readonly Mock<IRestauranteEstadisticasRepository> _estadisticasRepoMock;
        private readonly Mock<IUsuarioRestauranteFavoritoRepository> _favoritoRepoMock;
        private readonly Mock<IRestauranteRepository> _restauranteRepoMock;
        private readonly ObtenerMetricasRestauranteUseCase _sut;
        private readonly Guid _usuarioId = Guid.NewGuid();

        public ObtenerMetricasRestauranteUseCaseTests()
        {
            _estadisticasRepoMock = new Mock<IRestauranteEstadisticasRepository>();
            _favoritoRepoMock = new Mock<IUsuarioRestauranteFavoritoRepository>();
            _restauranteRepoMock = new Mock<IRestauranteRepository>();

            _sut = new ObtenerMetricasRestauranteUseCase(
                _estadisticasRepoMock.Object,
                _favoritoRepoMock.Object,
                _restauranteRepoMock.Object);
        }

      
        [Fact]
        public async Task HandleAsync_RestauranteIdVacio_LanzaArgumentException()
        {
            var restauranteId = Guid.Empty;

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                _sut.HandleAsync(restauranteId, _usuarioId, CancellationToken.None));

            Assert.StartsWith("El restauranteId no puede ser vacío.", ex.Message);
            Assert.Equal("restauranteId", ex.ParamName);
        }

     
        [Fact]
        public async Task HandleAsync_SinEstadisticas_LanzaKeyNotFoundException()
        {
            var restauranteId = Guid.NewGuid();

            _restauranteRepoMock
                .Setup(r => r.GetRestauranteByIdAsync(restauranteId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Restaurante { Id = restauranteId, DuenoId = _usuarioId });

            _estadisticasRepoMock
                .Setup(r => r.ObtenerPorRestauranteAsync(restauranteId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((RestauranteEstadisticas?)null);

            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _sut.HandleAsync(restauranteId, _usuarioId, CancellationToken.None));

            Assert.Equal("No se encontraron estadisticas con esa clave", ex.Message);

            _estadisticasRepoMock.Verify(
                r => r.ObtenerPorRestauranteAsync(restauranteId, It.IsAny<CancellationToken>()),
                Times.Once);

            _favoritoRepoMock.Verify(
                f => f.CountByRestauranteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_RestauranteDeOtroUsuario_LanzaAccesoProhibidoYNoConsultaMetricas()
        {
            var restauranteId = Guid.NewGuid();

            _restauranteRepoMock
                .Setup(r => r.GetRestauranteByIdAsync(restauranteId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Restaurante { Id = restauranteId, DuenoId = Guid.NewGuid() });

            var accion = () => _sut.HandleAsync(restauranteId, _usuarioId, CancellationToken.None);

            var excepcion = await Assert.ThrowsAsync<AccesoProhibidoException>(accion);

            Assert.Equal("No tenés permisos para consultar las métricas de este restaurante.", excepcion.Message);
            _estadisticasRepoMock.Verify(
                r => r.ObtenerPorRestauranteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _favoritoRepoMock.Verify(
                r => r.CountByRestauranteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_RestaurantePropio_DevuelveMetricasYFavoritos()
        {
            var restauranteId = Guid.NewGuid();
            var estadisticas = new RestauranteEstadisticas { RestauranteId = restauranteId };
            var favoritos = new List<UsuarioRestauranteFavorito>();

            _restauranteRepoMock
                .Setup(r => r.GetRestauranteByIdAsync(restauranteId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Restaurante { Id = restauranteId, DuenoId = _usuarioId });
            _estadisticasRepoMock
                .Setup(r => r.ObtenerPorRestauranteAsync(restauranteId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(estadisticas);
            _favoritoRepoMock
                .Setup(r => r.CountByRestauranteAsync(restauranteId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(favoritos);

            var resultado = await _sut.HandleAsync(restauranteId, _usuarioId, CancellationToken.None);

            Assert.Equal(restauranteId, resultado.RestauranteId);
            Assert.Same(estadisticas, resultado.Estadisticas);
            Assert.Same(favoritos, resultado.TotalFavoritos);
        }

   
        /*[Fact]
        public async Task HandleAsync_ConEstadisticasYFavoritos_DevuelveMetricasYLlamaRepos()
        {
            var restauranteId = Guid.NewGuid();
            var ct = CancellationToken.None;

            var estadisticas = new RestauranteEstadisticas(); 

            _estadisticasRepoMock
                .Setup(r => r.ObtenerPorRestauranteAsync(restauranteId, ct))
                .ReturnsAsync(estadisticas);

            var totalFavoritos = 7;

            _favoritoRepoMock
                .Setup(f => f.CountByRestauranteAsync(restauranteId, ct))
                .ReturnsAsync(totalFavoritos);

            var result = await _sut.HandleAsync(restauranteId, ct);

            Assert.NotNull(result);
            Assert.IsType<RestauranteMetricasRecord>(result);

            _estadisticasRepoMock.Verify(
                r => r.ObtenerPorRestauranteAsync(restauranteId, ct),
                Times.Once);

            _favoritoRepoMock.Verify(
                f => f.CountByRestauranteAsync(restauranteId, ct),
                Times.Once);
        }*/
    }
}
