using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Microsoft.EntityFrameworkCore;

namespace GustosApp.Infraestructure.Repositories
{
    public class RestauranteEstadisticasRepositoryEF : IRestauranteEstadisticasRepository
    {
        private readonly GustosDbContext _dbContext;

        public RestauranteEstadisticasRepositoryEF(GustosDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<RestauranteEstadisticas?> ObtenerPorRestauranteAsync(Guid restauranteId, CancellationToken ct = default)
        {
            return await _dbContext.RestauranteEstadisticas
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.RestauranteId == restauranteId, ct);
        }

        public Task<List<ValoracionLocal>> ObtenerValoracionesLocalesAsync(Guid restauranteId, CancellationToken ct)
            => _dbContext.OpinionesRestaurantes.AsNoTracking()
                .Where(r => r.RestauranteId == restauranteId && !r.EsImportada)
                .Select(r => new ValoracionLocal(r.Valoracion, r.FechaCreacion))
                .ToListAsync(ct);

        public async Task IncrementarTop3IndividualAsync(IEnumerable<Guid> restauranteIds, CancellationToken ct = default)
        {
            await IncrementarContadorAsync(restauranteIds, e => e.IncrementarTop3Individual(), 1, ct);
        }

        public async Task IncrementarTop3GrupoAsync(IEnumerable<Guid> restauranteIds, CancellationToken ct = default)
        {
            await IncrementarContadorAsync(restauranteIds, e => e.IncrementarTop3Grupo(), 2, ct);
        }

        public async Task IncrementarVisitaPerfilAsync(Guid restauranteId, CancellationToken ct = default)
        {
            await IncrementarContadorAsync(new[] { restauranteId }, e => e.IncrementarVisitaPerfil(), 3, ct);
        }

        public async Task RegistrarVistaPublicaAsync(Guid restauranteId, Guid usuarioId, CancellationToken ct)
        {
            if (!_dbContext.Database.IsSqlServer()) return;
            await new Services.EfApplicationTransaction(_dbContext).ExecuteAsync(async token =>
            {
                var ahora = DateTime.UtcNow;
                var aceptada = await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    MERGE dbo.VentanasVistaRestaurante WITH (HOLDLOCK) AS destino
                    USING (
                        SELECT Id FROM dbo.Restaurantes
                        WHERE Id = {restauranteId}
                          AND (DuenoId IS NULL OR DuenoId <> {usuarioId})
                          AND PropietarioUid <> (SELECT FirebaseUid FROM dbo.Usuarios WHERE Id = {usuarioId})
                    ) AS origen
                    ON destino.RestauranteId = origen.Id AND destino.UsuarioId = {usuarioId}
                    WHEN MATCHED AND destino.FechaUtc <= {ahora.AddMinutes(-30)} THEN
                        UPDATE SET FechaUtc = {ahora}
                    WHEN NOT MATCHED THEN INSERT (RestauranteId, UsuarioId, FechaUtc)
                        VALUES ({restauranteId}, {usuarioId}, {ahora});
                    """, token);
                if (aceptada == 1) await IncrementarVisitaPerfilAsync(restauranteId, token);
                return aceptada;
            }, ct);
        }

        private async Task IncrementarContadorAsync(
            IEnumerable<Guid> restauranteIds,
            Action<RestauranteEstadisticas> accion,
            int contador,
            CancellationToken ct)
        {
            var ids = restauranteIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (!ids.Any())
                return;

            if (_dbContext.Database.IsSqlServer())
            {
                // HOLDLOCK serializa también la inserción si todavía no existe la fila.
                foreach (var id in ids.OrderBy(id => id))
                {
                    var ahora = DateTime.UtcNow;
                    var individual = contador == 1 ? 1 : 0;
                    var grupo = contador == 2 ? 1 : 0;
                    var visita = contador == 3 ? 1 : 0;
                    await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                        MERGE dbo.RestauranteEstadisticas WITH (HOLDLOCK) AS destino
                        USING (SELECT {id} AS RestauranteId) AS origen
                        ON destino.RestauranteId = origen.RestauranteId
                        WHEN MATCHED THEN UPDATE SET
                            TotalTop3Individual = destino.TotalTop3Individual + {individual},
                            TotalTop3Grupo = destino.TotalTop3Grupo + {grupo},
                            TotalVisitasPerfil = destino.TotalVisitasPerfil + {visita},
                            UltimaActualizacionUtc = {ahora}
                        WHEN NOT MATCHED THEN INSERT
                            (RestauranteId, TotalTop3Individual, TotalTop3Grupo, TotalVisitasPerfil, FechaCreacionUtc, UltimaActualizacionUtc)
                        VALUES ({id}, {individual}, {grupo}, {visita}, {ahora}, {ahora});
                        """, ct);
                }
                return;
            }

            var existentes = await _dbContext.RestauranteEstadisticas
                .Where(e => ids.Contains(e.RestauranteId))
                .ToListAsync(ct);

            var existentesPorId = existentes.ToDictionary(e => e.RestauranteId, e => e);

            foreach (var id in ids)
            {
                if (!existentesPorId.TryGetValue(id, out var estadisticas))
                {
                    estadisticas = new RestauranteEstadisticas
                    {
                        RestauranteId = id,
                        FechaCreacionUtc = DateTime.UtcNow,
                        UltimaActualizacionUtc = DateTime.UtcNow
                    };

                    accion(estadisticas);
                    _dbContext.RestauranteEstadisticas.Add(estadisticas);
                    existentesPorId[id] = estadisticas;
                }
                else
                {
                    accion(estadisticas);
                    estadisticas.UltimaActualizacionUtc = DateTime.UtcNow;
                }
            }

            await _dbContext.SaveChangesAsync(ct);
        }
    }
}
