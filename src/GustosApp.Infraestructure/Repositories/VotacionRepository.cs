using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Infraestructure;
using Microsoft.EntityFrameworkCore;
using GustosApp.Infraestructure.Services;
using Microsoft.Data.SqlClient;

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
            try
            {
                await GuardarDisponiblesAsync(votacion.RestaurantesCandidatos.Select(c => c.RestauranteId), async () =>
                {
                    _context.Votaciones.Add(votacion);
                    await _context.SaveChangesAsync(ct);
                }, ct);
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
            await GuardarDisponiblesAsync([voto.RestauranteId], async () =>
            {
                await ExigirVotacionActivaAsync(voto.VotacionId, ct);
                _context.Votos.Add(voto);
                await _context.SaveChangesAsync(ct);
            }, ct);
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
            await GuardarDisponiblesAsync(votacion.RestauranteGanadorId.HasValue ? [votacion.RestauranteGanadorId.Value] : [], async () =>
            {
                await ExigirVotacionActivaAsync(votacion.Id, ct);
                _context.Entry(votacion).State = EntityState.Modified;
                await _context.SaveChangesAsync(ct);
            }, ct);
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
            await GuardarDisponiblesAsync([voto.RestauranteId], async () =>
            {
                await ExigirVotacionActivaAsync(voto.VotacionId, ct);
                _context.Entry(voto).State = EntityState.Modified;
                await _context.SaveChangesAsync(ct);
            }, ct);
        }

        private async Task ExigirVotacionActivaAsync(Guid id, CancellationToken ct)
        {
            if (!await _context.Votaciones.AsNoTracking().AnyAsync(v => v.Id == id && v.Estado == EstadoVotacion.Activa, ct))
                throw new InvalidOperationException("La votación ya no está activa. Recargá sus resultados.");
        }

        private async Task<bool> GuardarDisponiblesAsync(IEnumerable<Guid> ids, Func<Task> guardar, CancellationToken ct)
        {
            try { return await new EfApplicationTransaction(_context).ExecuteAsync(async token =>
            {
                var unicos = ids.Distinct().ToArray();
                // La lectura y la escritura comparten la transacción serializable con la baja.
                // AsNoTracking evita aceptar la copia antigua de una ficha ya cargada en el scope.
                if (await _context.Restaurantes.AsNoTracking().CountAsync(r => unicos.Contains(r.Id) && !r.Desactivado, token) != unicos.Length)
                    throw new InvalidOperationException("Un restaurante ya no está disponible. Recargá la votación; podés cancelarla e iniciar otra conservando su historial.");
                await guardar();
                return true;
            }, ct); }
            catch (Exception ex) when (ex is SqlException { Number: 1205 } || ex.InnerException is SqlException { Number: 1205 })
            { throw new InvalidOperationException("La votación o la disponibilidad cambiaron. Recargá los resultados antes de continuar."); }
        }

        public async Task RecargarVotacionAsync(VotacionGrupo votacion, CancellationToken ct = default)
        {
            await _context.Entry(votacion).ReloadAsync(ct);
            await _context.Entry(votacion).Collection(v => v.Participantes).Query().LoadAsync(ct);
            await _context.Entry(votacion).Collection(v => v.Votos).Query().LoadAsync(ct);
        }

    }
}
