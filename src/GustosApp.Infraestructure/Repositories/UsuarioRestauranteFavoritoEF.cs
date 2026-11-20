using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GustosApp.Infraestructure.Repositories
{
    public class UsuarioRestauranteFavoritoEF : IUsuarioRestauranteFavoritoRepository
    {
        private readonly GustosDbContext _dbContext;

        public UsuarioRestauranteFavoritoEF(GustosDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<int> CountByUsuarioAsync(Guid usuarioId, CancellationToken ct)
        {
            return await _dbContext.UsuarioRestauranteFavoritos
                .CountAsync(x => x.UsuarioId == usuarioId, ct);
        }

        public async Task CrearAsync(UsuarioRestauranteFavorito usuarioRestauranteFavorito, CancellationToken ct)
        {
            await _dbContext.UsuarioRestauranteFavoritos.AddAsync(usuarioRestauranteFavorito, ct);
            await _dbContext.SaveChangesAsync(ct);
        }

        public async Task<ResultadoAgregarFavorito> AgregarSiCorrespondeAsync(
            Guid usuarioId,
            Guid restauranteId,
            int? limite,
            DateTime fechaAgregado,
            CancellationToken ct)
        {
            if (!_dbContext.Database.IsRelational())
                return await AgregarInternoAsync(usuarioId, restauranteId, limite, fechaAgregado, ct);

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct);
            try
            {
                var resultado = await AgregarInternoAsync(
                    usuarioId,
                    restauranteId,
                    limite,
                    fechaAgregado,
                    ct);
                await transaction.CommitAsync(ct);
                return resultado;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        private async Task<ResultadoAgregarFavorito> AgregarInternoAsync(
            Guid usuarioId,
            Guid restauranteId,
            int? limite,
            DateTime fechaAgregado,
            CancellationToken ct)
        {
            if (await ExistsAsync(usuarioId, restauranteId, ct))
                return ResultadoAgregarFavorito.YaExistia;

            if (limite.HasValue && await CountByUsuarioAsync(usuarioId, ct) >= limite.Value)
                return ResultadoAgregarFavorito.LimiteAlcanzado;

            var favorito = new UsuarioRestauranteFavorito
            {
                UsuarioId = usuarioId,
                RestauranteId = restauranteId,
                FechaAgregado = fechaAgregado
            };
            await _dbContext.UsuarioRestauranteFavoritos.AddAsync(favorito, ct);

            try
            {
                await _dbContext.SaveChangesAsync(ct);
                return ResultadoAgregarFavorito.Agregado;
            }
            catch (DbUpdateException)
            {
                _dbContext.Entry(favorito).State = EntityState.Detached;
                if (await ExistsAsync(usuarioId, restauranteId, ct))
                    return ResultadoAgregarFavorito.YaExistia;
                throw;
            }
        }

        public async Task<bool> ExistsAsync(Guid usuarioId, Guid restauranteId, CancellationToken ct)
        {
           return await _dbContext.UsuarioRestauranteFavoritos
                .AnyAsync(x => x.UsuarioId == usuarioId && x.RestauranteId == restauranteId, ct);
        }

        public async Task<List<UsuarioRestauranteFavorito>> CountByRestauranteAsync(Guid restauranteId, CancellationToken ct)
        {
            return await _dbContext.UsuarioRestauranteFavoritos
                .Where(x => x.RestauranteId == restauranteId)
                .ToListAsync(ct);
        }


        public async Task EliminarAsync(Guid usuarioId, Guid restauranteId, CancellationToken ct)
        {
            var favorito = await _dbContext.UsuarioRestauranteFavoritos
                    .FirstOrDefaultAsync(f =>
                        f.UsuarioId == usuarioId &&
                        f.RestauranteId == restauranteId,
                        ct);

            if (favorito != null)
            {
                _dbContext.UsuarioRestauranteFavoritos.Remove(favorito);
                await _dbContext.SaveChangesAsync(ct);
            }
        }

        public async Task<List<Restaurante>> GetFavoritosByUsuarioAsync(Guid usuarioId, CancellationToken ct)
        {
            return await _dbContext.UsuarioRestauranteFavoritos
                .Where(f => f.UsuarioId == usuarioId)
                .Include(f => f.Restaurante)
                .Select(f => f.Restaurante)
                .ToListAsync(ct);
        }
    }
   
}
