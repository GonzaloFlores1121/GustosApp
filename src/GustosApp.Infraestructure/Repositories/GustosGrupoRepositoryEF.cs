using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GustosApp.Infraestructure.Repositories
{
    public class GustosGrupoRepositoryEF : IGustosGrupoRepository
    {

        private GustosDbContext _context;

        public GustosGrupoRepositoryEF(GustosDbContext context)
        {
            _context = context;
        }

        public async Task<bool> AgregarGustosAlGrupo(Guid grupoId, List<Gusto> gustos, Guid idMiembro)
        {
            var nuevosGustos = gustos
                .Select(g => new GrupoGusto
                {
                    Id = Guid.NewGuid(),
                    GrupoId = grupoId,
                    GustoId = g.Id,
                    MiembroId = idMiembro
                })
                .ToList();

            if (!nuevosGustos.Any())
                return true;

            await _context.GrupoGustos.AddRangeAsync(nuevosGustos);
            await _context.SaveChangesAsync();

            return true;
        }


        public async Task<bool> EliminarGustosAlGrupo(Guid grupoId, List<Gusto> gustos, Guid miembroGrupoId)
        {
            int eliminados = 0;

            foreach (var gusto in gustos)
            {
                var entidad = await _context.GrupoGustos
                    .FirstOrDefaultAsync(gg => gg.GrupoId == grupoId && gg.GustoId == gusto.Id && gg.MiembroId==miembroGrupoId);

                if (entidad != null)
                {
                    _context.GrupoGustos.Remove(entidad);
                    eliminados++;
                }
            }

            if (eliminados == 0)
                throw new Exception("Ninguno de los gustos que intenta eliminar existe en el grupo.");

            await _context.SaveChangesAsync();
            return eliminados > 0;
        }


        public async Task<List<string>> ObtenerGustosDelGrupo(Guid grupoId)
        {
            var gustos = await _context.MiembrosGrupos
                .AsNoTracking()
                .Where(m => m.GrupoId == grupoId && m.ParticipaEnRecomendacion && m.Activo)
                .SelectMany(m => m.Usuario.Gustos.Select(g => g.Nombre))
                .Distinct()
                .ToListAsync();

            return gustos;
        }

        public async Task<List<Guid>> ObtenerGustosIdsDelGrupo(Guid grupoId)
        {
            var gustosIds = await _context.MiembrosGrupos
                .AsNoTracking()
                .Where(m => m.GrupoId == grupoId && m.ParticipaEnRecomendacion && m.Activo)
                .SelectMany(m => m.Usuario.Gustos.Select(g => g.Id))
                .Distinct()
                .ToListAsync();

            return gustosIds;
        }

    }
}
