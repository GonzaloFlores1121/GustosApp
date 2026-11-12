using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Infraestructure;
using Microsoft.EntityFrameworkCore;

namespace GustosApp.Infraestructure.Repositories
{
    public class VotacionRepository : IVotacionRepository
    {
        private readonly GustosDbContext _context;

        public VotacionRepository(GustosDbContext context)
        {
            _context = context;
        }

        public async Task<VotacionGrupo> CrearVotacionAsync(VotacionGrupo votacion, CancellationToken ct = default)
        {
            _context.Votaciones.Add(votacion);
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (
                ex.InnerException?.Message.Contains(
                    "IX_Votaciones_GrupoId",
                    StringComparison.Ordinal) == true)
            {
                throw new InvalidOperationException(
                    "Ya existe una votación activa en este grupo",
                    ex);
            }
            return votacion;
        }

        public async Task<VotacionGrupo?> ObtenerPorIdAsync(Guid votacionId, CancellationToken ct = default)
        {
            return await _context.Votaciones
                .Include(v => v.Participantes)
                .Include(v => v.Votos)
                    .ThenInclude(vo => vo.Usuario)
                .Include(v => v.Votos)
                    .ThenInclude(vo => vo.Restaurante)
                .Include(v => v.Grupo)
                    .ThenInclude(g => g.Miembros)
                        .ThenInclude(m => m.Usuario)
                .FirstOrDefaultAsync(v => v.Id == votacionId, ct);
        }

        public async Task<VotacionGrupo?> ObtenerVotacionActivaAsync(Guid grupoId, CancellationToken ct = default)
        {
            return await _context.Votaciones
                .Include(v => v.Participantes)
                    .ThenInclude(p => p.Usuario)
                        .ThenInclude(u => u.Restricciones)
                            .ThenInclude(r => r.TagsProhibidos)
                .Include(v => v.Participantes)
                    .ThenInclude(p => p.Usuario)
                        .ThenInclude(u => u.CondicionesMedicas)
                            .ThenInclude(c => c.TagsCriticos)
                .Include(v => v.RestaurantesCandidatos)
                    .ThenInclude(rc => rc.Restaurante)
                        .ThenInclude(r => r.GustosQueSirve)
                            .ThenInclude(g => g.Tags)
                .Include(v => v.RestaurantesCandidatos)
                    .ThenInclude(rc => rc.Restaurante)
                        .ThenInclude(r => r.RestriccionesQueRespeta)
                .Include(v => v.Votos)
                    .ThenInclude(vo => vo.Usuario)
                .Include(v => v.Votos)
                    .ThenInclude(vo => vo.Restaurante)
                .Include(v => v.Grupo)
                    .ThenInclude(g => g.Miembros)
                        .ThenInclude(m => m.Usuario)
                .AsSplitQuery()
                .Where(v => v.GrupoId == grupoId && v.Estado == EstadoVotacion.Activa)
                .OrderByDescending(v => v.FechaInicio)
                .FirstOrDefaultAsync(ct);
        }


        public async Task<(IReadOnlyList<VotacionGrupo> Votaciones, int Total)> ObtenerHistorialVotacionesAsync(
            Guid grupoId,
            int pagina,
            int tamanoPagina,
            CancellationToken ct = default)
        {
            var consulta = _context.Votaciones
                .AsNoTracking()
                .AsSplitQuery()
                .Include(v => v.Participantes)
                .Include(v => v.Votos)
                .Include(v => v.RestauranteGanador)
                .Where(v => v.GrupoId == grupoId &&
                    (v.Estado == EstadoVotacion.Cerrada || v.Estado == EstadoVotacion.Cancelada));

            var total = await consulta.CountAsync(ct);
            var votaciones = await consulta
                .OrderByDescending(v => v.FechaInicio)
                .Skip((pagina - 1) * tamanoPagina)
                .Take(tamanoPagina)
                .ToListAsync(ct);

            return (votaciones, total);
        }

        public async Task<VotoRestaurante> RegistrarVotoAsync(VotoRestaurante voto, CancellationToken ct = default)
        {
            _context.Votos.Add(voto);
            await _context.SaveChangesAsync(ct);
            return voto;
        }

        public async Task<VotoRestaurante?> ObtenerVotoUsuarioAsync(Guid votacionId, Guid usuarioId, CancellationToken ct = default)
        {
            return await _context.Votos
                .Include(v => v.Restaurante)
                .FirstOrDefaultAsync(v => v.VotacionId == votacionId && v.UsuarioId == usuarioId, ct);
        }

        public async Task ActualizarVotacionAsync(VotacionGrupo votacion, CancellationToken ct = default)
        {
            _context.Votaciones.Update(votacion);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<Dictionary<Guid, int>> ObtenerResultadosAsync(Guid votacionId, CancellationToken ct = default)
        {
            var votos = await _context.Votos
                .Where(v => v.VotacionId == votacionId)
                .ToListAsync(ct);

            return votos
                .GroupBy(v => v.RestauranteId)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public async Task<bool> UsuarioYaVotoAsync(Guid votacionId, Guid usuarioId, CancellationToken ct = default)
        {
            return await _context.Votos
                .AnyAsync(v => v.VotacionId == votacionId && v.UsuarioId == usuarioId, ct);
        }

        public async Task<VotacionGrupo?> ObtenerPorIdConCandidatosAsync(Guid votacionId, CancellationToken ct = default)
        {
            return await _context.Votaciones
                .Include(v => v.Participantes)
                .Include(v => v.RestaurantesCandidatos)
                    .ThenInclude(rc => rc.Restaurante)
                .Include(v => v.Votos)
                    .ThenInclude(vo => vo.Usuario)
                .Include(v => v.Votos)
                    .ThenInclude(vo => vo.Restaurante)
                .Include(v => v.Grupo)
                    .ThenInclude(g => g.Miembros)
                        .ThenInclude(m => m.Usuario)
                .FirstOrDefaultAsync(v => v.Id == votacionId, ct);
        }


        public async Task ActualizarVotoAsync(VotoRestaurante voto, CancellationToken ct = default)
        {
            _context.Votos.Update(voto);
            await _context.SaveChangesAsync(ct);
        }

        public async Task RecargarVotacionAsync(VotacionGrupo votacion, CancellationToken ct = default)
        {
            await _context.Entry(votacion).ReloadAsync(ct);
            await _context.Entry(votacion).Collection(v => v.Participantes).Query().LoadAsync(ct);
            await _context.Entry(votacion).Collection(v => v.Votos).Query().LoadAsync(ct);
        }

    }
}
