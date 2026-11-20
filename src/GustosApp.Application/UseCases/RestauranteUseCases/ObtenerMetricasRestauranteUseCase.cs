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

            estadisticas ??= new() { RestauranteId = restauranteId };

            var totalFavoritos = await _favoritoRepository.CountByRestauranteAsync(restauranteId, ct);

            var valoraciones = await _estadisticasRepository.ObtenerValoracionesLocalesAsync(restauranteId, ct) ?? [];
            var zona = TimeZoneInfo.FindSystemTimeZoneById(GustosApp.Domain.Common.HorariosRestaurante.ZonaHoraria);
            DateOnly DiaLocal(DateTime utc) => DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zona));
            var hoy = DiaLocal(DateTime.UtcNow);
            var porDia = valoraciones.GroupBy(v => DiaLocal(v.FechaUtc)).ToDictionary(g => g.Key, g => g.Count());
            var actividad = Enumerable.Range(0, 7).Select(i => hoy.AddDays(i - 6))
                .Select(fecha => new ActividadResenas(fecha, porDia.GetValueOrDefault(fecha))).ToArray();
            var distribucion = Enumerable.Range(1, 5)
                .Select(estrellas => valoraciones.Count(v => Math.Round(v.Valoracion, MidpointRounding.AwayFromZero) == estrellas)).ToArray();
            var resumen = new ResumenResenasLocales(valoraciones.Count,
                valoraciones.Count == 0 ? null : valoraciones.Average(v => v.Valoracion), distribucion, actividad);
            return new RestauranteMetricasRecord(restauranteId, estadisticas, totalFavoritos, resumen);
        }
    }
}
