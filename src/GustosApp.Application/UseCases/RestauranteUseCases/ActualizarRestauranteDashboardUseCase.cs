using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;

namespace GustosApp.Application.UseCases.RestauranteUseCases
{
    public class ActualizarRestauranteDashboardUseCase
    {
        private readonly IRestauranteRepository _restauranteRepository;
        private readonly IGustoRepository _gustoRepository;
        private readonly IRestriccionRepository _restriccionRepository;
        private readonly TimeProvider _reloj;

        public ActualizarRestauranteDashboardUseCase(
            IRestauranteRepository restauranteRepository,
            IGustoRepository gustoRepository,
            IRestriccionRepository restriccionRepository,
            TimeProvider? reloj = null)
        {
            _restauranteRepository = restauranteRepository;
            _gustoRepository = gustoRepository;
            _restriccionRepository = restriccionRepository;
            _reloj = reloj ?? TimeProvider.System;
        }

        public async Task<Restaurante> HandleAsync(
            Guid restauranteId,
            Guid usuarioId,
            string? direccion,
            double? latitud,
            double? longitud,
            string? horariosJson,
            string? webUrl,
            List<Guid>? gustosQueSirveIds,
            List<Guid>? restriccionesQueRespetaIds,
            CancellationToken ct = default,
            bool webUrlFueEnviado = false)
        {
            var restaurante = await _restauranteRepository.GetByIdAsync(restauranteId, ct);
            if (restaurante == null)
                throw new InvalidOperationException("El restaurante no existe.");

            if (restaurante.DuenoId != usuarioId)
                throw new AccesoProhibidoException("No tenés permisos para actualizar este restaurante.");

            // Dirección
            if (!string.IsNullOrWhiteSpace(direccion))
                restaurante.Direccion = direccion;

            // Coordenadas
            if (latitud.HasValue)
                restaurante.Latitud = latitud.Value;

            if (longitud.HasValue)
                restaurante.Longitud = longitud.Value;

            // Horarios (si viene null, no tocamos; si viene string, lo pisamos tal cual)
            if (horariosJson != null)
                restaurante.HorariosJson = horariosJson;

            // WebUrl: si viene null no tocamos; si viene string vacío, lo seteamos en null
            if (webUrlFueEnviado || webUrl != null)
                restaurante.WebUrl = string.IsNullOrWhiteSpace(webUrl) ? null : webUrl;

            // Gustos
            if (gustosQueSirveIds != null)
            {
                var idsAnteriores = restaurante.GustosQueSirve.Select(g => g.Id).ToArray();
                var gustos = gustosQueSirveIds.Count > 0
                    ? await _gustoRepository.GetByIdsAsync(gustosQueSirveIds, ct)
                    : new List<Gusto>();

                restaurante.SetGustos(gustos);
                restaurante.RegistrarCambioOferta(idsAnteriores, _reloj.GetUtcNow().UtcDateTime);
            }

            // Restricciones
            if (restriccionesQueRespetaIds != null)
            {
                var idsAnteriores = restaurante.RestriccionesQueRespeta.Select(r => r.Id).ToArray();
                var restricciones = restriccionesQueRespetaIds.Count > 0
                    ? await _restriccionRepository.GetRestriccionesByIdsAsync(restriccionesQueRespetaIds, ct)
                    : new List<Restriccion>();

                restaurante.SetRestricciones(restricciones);
                restaurante.RegistrarCambioRestricciones(idsAnteriores, _reloj.GetUtcNow().UtcDateTime);
            }

            restaurante.ActualizadoUtc = DateTime.UtcNow;
            restaurante.UltimaActualizacion = DateTime.UtcNow;

            await _restauranteRepository.UpdateAsync(restaurante, ct);
            await _restauranteRepository.SaveChangesAsync(ct);

            return restaurante;
        }
    }
}
