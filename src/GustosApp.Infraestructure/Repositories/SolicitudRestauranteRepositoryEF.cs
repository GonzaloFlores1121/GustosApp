using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model.@enum;
using GustosApp.Domain.Model;
using Microsoft.EntityFrameworkCore;

namespace GustosApp.Infraestructure.Repositories
{

    public class SolicitudRestauranteRepositoryEF : ISolicitudRestauranteRepository
    {
        private readonly GustosDbContext _db;

        public SolicitudRestauranteRepositoryEF(GustosDbContext db) => _db = db;

        public Task<SolicitudRestaurante?> BuscarReclamoPendienteAsync(Guid usuarioId, Guid restauranteId, CancellationToken ct)
            => _db.SolicitudesRestaurantes.FirstOrDefaultAsync(s => s.UsuarioId == usuarioId
                && s.RestauranteExistenteId == restauranteId && s.Estado == EstadoSolicitudRestaurante.Pendiente, ct);

        public Task<SolicitudRestaurante?> BuscarPendientePorUsuarioAsync(Guid usuarioId, CancellationToken ct)
            => _db.SolicitudesRestaurantes.AsNoTracking()
                .FirstOrDefaultAsync(s => s.UsuarioId == usuarioId && s.Estado == EstadoSolicitudRestaurante.Pendiente, ct);

        public Task<SolicitudRestaurante?> BuscarUltimaPorUsuarioAsync(Guid usuarioId, CancellationToken ct)
            => _db.SolicitudesRestaurantes.AsNoTracking()
                .Where(s => s.UsuarioId == usuarioId)
                .OrderByDescending(s => s.FechaCreacion)
                .FirstOrDefaultAsync(ct);

        public Task<SolicitudRestaurante?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            return _db.SolicitudesRestaurantes
                .Include(s => s.Usuario)
                .Include(s => s.Imagenes)
                .Include(g => g.Gustos)
                .Include(r => r.Restricciones)
                .FirstOrDefaultAsync(s => s.Id == id, ct);
        }

        public Task<List<SolicitudRestaurante>> GetPendientesAsync(CancellationToken ct)
        {
            return _db.SolicitudesRestaurantes
                .Include(s => s.Usuario)
                .Include(s => s.Imagenes)
                .Where(s => s.Estado == EstadoSolicitudRestaurante.Pendiente)
                .ToListAsync(ct);
        }

        public async Task AddAsync(SolicitudRestaurante solicitud, CancellationToken ct)
        {
            _db.SolicitudesRestaurantes.Add(solicitud);
            try { await _db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("El usuario o la solicitud cambiaron durante la operación. Volvé a consultar su estado.");
            }
            catch (DbUpdateException)
            {
                throw new InvalidOperationException("Ya existe una solicitud de restaurante pendiente para este usuario.");
            }
        }

        public async Task UpdateAsync(SolicitudRestaurante solicitud, CancellationToken ct)
        {
            _db.SolicitudesRestaurantes.Update(solicitud);
            try { await _db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("La solicitud cambió durante la operación. Volvé a consultar su estado.");
            }
        }

        public async Task<IEnumerable<SolicitudRestaurante>> GetAllAsync(CancellationToken ct)
        {
            return await _db.SolicitudesRestaurantes
                .Include(s => s.Usuario)
                .Include(s => s.Gustos)
                .Include(s => s.Restricciones)
                .Include(s => s.Imagenes)
                .OrderByDescending(s => s.FechaCreacion)
                .ToListAsync(ct);
        }

        public async Task<IEnumerable<SolicitudRestaurante>> GetByEstadoAsync(
            EstadoSolicitudRestaurante estado,
            CancellationToken ct)
        {
            return await _db.SolicitudesRestaurantes
                .Where(s => s.Estado == estado)
                .Include(s => s.Usuario)
                .Include(s => s.Gustos)
                .Include(s => s.Restricciones)
                .Include(s => s.Imagenes)
                .OrderByDescending(s => s.FechaCreacion)
                .ToListAsync(ct);
        }

    }

}
