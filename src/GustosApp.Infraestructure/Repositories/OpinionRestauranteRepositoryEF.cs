using GustosApp.Application.Interfaces;
using GustosApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GustosApp.Infraestructure.Repositories
{
    public class OpinionRestauranteRepositoryEF : IOpinionRestauranteRepository
    {
        private readonly GustosDbContext _context;

        public OpinionRestauranteRepositoryEF(GustosDbContext context)
        {
            _context = context;
        }
        public async Task CrearAsync(OpinionRestaurante opinionRestaurante, CancellationToken cancellationToken)
        {
            await _context.OpinionesRestaurantes.AddAsync(opinionRestaurante, cancellationToken);
            await GuardarYRecalcularAsync(opinionRestaurante.RestauranteId, cancellationToken);
        }

        public async Task ActualizarAsync(OpinionRestaurante opinionRestaurante, CancellationToken cancellationToken)
        {
            await GuardarYRecalcularAsync(opinionRestaurante.RestauranteId, cancellationToken);
        }

        public async Task EliminarAsync(OpinionRestaurante opinionRestaurante, CancellationToken cancellationToken)
        {
            _context.OpinionesRestaurantes.Remove(opinionRestaurante);
            await GuardarYRecalcularAsync(opinionRestaurante.RestauranteId, cancellationToken);
        }

        public async Task<bool> ExisteValoracionAsync(Guid usuarioId, Guid restauranteId, CancellationToken cancellationToken)
        {
            return await _context.OpinionesRestaurantes
                            .AnyAsync(v => v.UsuarioId == usuarioId && v.RestauranteId == restauranteId, cancellationToken);
        }

        public Task<OpinionRestaurante?> ObtenerPorUsuarioYRestauranteAsync(Guid usuarioId, Guid restauranteId, CancellationToken cancellationToken)
            => _context.OpinionesRestaurantes
                .AsNoTracking()
                .Include(o => o.Fotos)
                .Include(o => o.Usuario)
                .FirstOrDefaultAsync(o => o.UsuarioId == usuarioId && o.RestauranteId == restauranteId, cancellationToken);

        public Task<OpinionRestaurante?> ObtenerPorIdAsync(Guid opinionId, CancellationToken cancellationToken)
            => _context.OpinionesRestaurantes
                .Include(o => o.Fotos)
                .FirstOrDefaultAsync(o => o.Id == opinionId, cancellationToken);

        public async Task<List<OpinionRestaurante>> ObtenerPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken)
         {
             return await _context.OpinionesRestaurantes
                            .Where(n => n.UsuarioId == usuarioId)
                            .Include(v => v.Restaurante)
                            .Include(v => v.Usuario) 
                            .ToListAsync(cancellationToken);
        }

        public async Task<List<OpinionRestaurante>> ObtenerPorRestauranteAsync(Guid restauranteId, CancellationToken cancellationToken)
        {
            return await _context.OpinionesRestaurantes
                .Where(v => v.RestauranteId == restauranteId)
                .ToListAsync(cancellationToken);
        }

        public Task<bool> FotoEstaReferenciadaAsync(string url, CancellationToken cancellationToken)
            => _context.OpinionesFotos.AsNoTracking().AnyAsync(f => f.Url == url, cancellationToken);

        private async Task GuardarYRecalcularAsync(Guid restauranteId, CancellationToken cancellationToken)
        {
            IDbContextTransaction? transaction = null;
            if (_context.Database.IsRelational() && _context.Database.CurrentTransaction is null)
                transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);

                var promedioLocal = await _context.OpinionesRestaurantes
                    .Where(o => o.RestauranteId == restauranteId && !o.EsImportada && o.UsuarioId.HasValue)
                    .Select(o => (double?)o.Valoracion)
                    .AverageAsync(cancellationToken) ?? 0;

                var restaurante = await _context.Restaurantes.FindAsync(new object[] { restauranteId }, cancellationToken);
                if (restaurante != null)
                {
                    restaurante.Valoracion = (decimal)promedioLocal;
                    await _context.SaveChangesAsync(cancellationToken);
                }

                if (transaction != null)
                    await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (EsConflictoDeUnicidad(ex))
            {
                if (transaction != null)
                    await transaction.RollbackAsync(cancellationToken);
                throw new InvalidOperationException("Ya tenés una reseña publicada para este restaurante.", ex);
            }
            finally
            {
                if (transaction != null)
                    await transaction.DisposeAsync();
            }
        }

        private static bool EsConflictoDeUnicidad(DbUpdateException exception)
            => exception.InnerException is SqlException sqlException
               && sqlException.Number is 2601 or 2627;

        
    }
}
