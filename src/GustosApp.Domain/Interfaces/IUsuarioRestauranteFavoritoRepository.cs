using GustosApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GustosApp.Domain.Interfaces
{
    public enum ResultadoAgregarFavorito
    {
        Agregado,
        YaExistia,
        LimiteAlcanzado
    }

    public interface IUsuarioRestauranteFavoritoRepository
    {
        Task<int> CountByUsuarioAsync(Guid usuarioId, CancellationToken ct);
        Task CrearAsync(UsuarioRestauranteFavorito usuarioRestauranteFavorito, CancellationToken ct);
        Task<bool> ExistsAsync(Guid usuarioId, Guid restauranteId, CancellationToken ct);
        Task<List<UsuarioRestauranteFavorito>> CountByRestauranteAsync(Guid restauranteId, CancellationToken ct);
        Task EliminarAsync(Guid id, Guid restauranteId, CancellationToken ct);
        Task<List<Restaurante>> GetFavoritosByUsuarioAsync(Guid id, CancellationToken ct);
        Task<ResultadoAgregarFavorito> AgregarSiCorrespondeAsync(
            Guid usuarioId,
            Guid restauranteId,
            int? limite,
            DateTime fechaAgregado,
            CancellationToken ct);
    }
}
