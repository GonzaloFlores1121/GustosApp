using System;
using System.Threading;
using System.Threading.Tasks;
using GustosApp.Domain.Interfaces;
using GustosApp.Application.record;
using GustosApp.Application.Common.Exceptions;
using System.Data;

namespace GustosApp.Application.UseCases.RestauranteUseCases
{
    public class ObtenerMetricasRestauranteUseCase
    {
        private readonly IRestauranteEstadisticasRepository _estadisticasRepository;
        private readonly IUsuarioRestauranteFavoritoRepository _favoritoRepository;
        private readonly IRestauranteRepository _restauranteRepository;

        public ObtenerMetricasRestauranteUseCase(
            IRestauranteEstadisticasRepository estadisticasRepository,
            IUsuarioRestauranteFavoritoRepository favoritoRepository,
            IRestauranteRepository restauranteRepository)
        {
            _estadisticasRepository = estadisticasRepository;
            _favoritoRepository = favoritoRepository;
            _restauranteRepository = restauranteRepository;
        }

        public async Task<RestauranteMetricasRecord> HandleAsync(
            Guid restauranteId,
            Guid usuarioId,
            CancellationToken ct = default)
        {
            if (restauranteId == Guid.Empty)
                throw new ArgumentException("El restauranteId no puede ser vacío.", nameof(restauranteId));

            var restaurante = await _restauranteRepository.GetRestauranteByIdAsync(restauranteId, ct)
                ?? throw new NotFoundException("Restaurante no encontrado.");

            if (restaurante.DuenoId != usuarioId)
                throw new AccesoProhibidoException("No tenés permisos para consultar las métricas de este restaurante.");

            var estadisticas = await _estadisticasRepository.ObtenerPorRestauranteAsync(restauranteId, ct);

            if (estadisticas==null)
            {
                throw new KeyNotFoundException("No se encontraron estadisticas con esa clave");
            }

            var totalFavoritos = await _favoritoRepository.CountByRestauranteAsync(restauranteId, ct);

            return new RestauranteMetricasRecord( restauranteId , estadisticas, totalFavoritos);
        }
    }
}
