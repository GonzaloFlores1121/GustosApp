using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GustosApp.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GustosApp.Application.UseCases.RestauranteUseCases
{
    public class RegistrarTop3IndividualRestaurantesUseCase
    {
        private readonly IRestauranteEstadisticasRepository _estadisticasRepository;

        private readonly ILogger? _logger;

        public RegistrarTop3IndividualRestaurantesUseCase(
            IRestauranteEstadisticasRepository estadisticasRepository, ILogger<RegistrarTop3IndividualRestaurantesUseCase>? logger = null)
        {
            _estadisticasRepository = estadisticasRepository;
            _logger = logger;
        }

        public async Task HandleAsync(IEnumerable<Guid> restauranteIds, CancellationToken ct = default)
        {
            if (restauranteIds == null)
                throw new ArgumentNullException(nameof(restauranteIds));

            try { await _estadisticasRepository.IncrementarTop3IndividualAsync(restauranteIds, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException && !ct.IsCancellationRequested)
            { _logger?.LogWarning(ex, "No se pudo registrar una aparición en recomendaciones."); }
        }
    }
}
