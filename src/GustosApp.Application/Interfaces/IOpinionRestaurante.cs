using GustosApp.Domain.Model;

namespace GustosApp.Application.Interfaces
{
    public interface IOpinionRestauranteRepository
    {
        Task CrearAsync(OpinionRestaurante opinionRestaurante, CancellationToken cancellationToken);
        Task ActualizarAsync(OpinionRestaurante opinionRestaurante, CancellationToken cancellationToken);
        Task EliminarAsync(OpinionRestaurante opinionRestaurante, CancellationToken cancellationToken);
        Task<List<OpinionRestaurante>> ObtenerPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken);
        Task<OpinionRestaurante?> ObtenerPorUsuarioYRestauranteAsync(Guid usuarioId, Guid restauranteId, CancellationToken cancellationToken);
        Task<OpinionRestaurante?> ObtenerPorIdAsync(Guid opinionId, CancellationToken cancellationToken);
        Task<bool> ExisteValoracionAsync(Guid usuarioId, Guid restauranteId, CancellationToken cancellationToken);
        Task<List<OpinionRestaurante>> ObtenerPorRestauranteAsync(Guid restauranteId, CancellationToken cancellationToken);
        Task<bool> FotoEstaReferenciadaAsync(string url, CancellationToken cancellationToken);
    }
}
