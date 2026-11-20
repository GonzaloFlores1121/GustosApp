using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GustosApp.Domain.Model;

namespace GustosApp.Domain.Interfaces
{
    public sealed record ValoracionLocal(double Valoracion, DateTime FechaUtc);

    public interface IRestauranteEstadisticasRepository
    {
        Task<RestauranteEstadisticas?> ObtenerPorRestauranteAsync(Guid restauranteId, CancellationToken ct = default);

        Task<List<ValoracionLocal>> ObtenerValoracionesLocalesAsync(Guid restauranteId, CancellationToken ct);

        Task IncrementarTop3IndividualAsync(IEnumerable<Guid> restauranteIds, CancellationToken ct = default);

        Task IncrementarTop3GrupoAsync(IEnumerable<Guid> restauranteIds, CancellationToken ct = default);

        Task RegistrarVistaPublicaAsync(Guid restauranteId, Guid usuarioId, CancellationToken ct);

        Task IncrementarVisitaPerfilAsync(Guid restauranteId, CancellationToken ct = default);
    }
}
