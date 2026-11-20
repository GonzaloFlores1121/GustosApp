using GustosApp.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using GustosApp.Domain.Model;
using GustosApp.Infraestructure.Repositories;

namespace GustosApp.API.Tests;

public class EtiquetasCondicionesPruebas
{
    [Fact]
    public async Task CatalogoVegetarianoYVegano_IncluyeMariscosComoEtiquetaCritica()
    {
        await using var contexto = DbContextEnMemoria.Crear(nameof(CatalogoVegetarianoYVegano_IncluyeMariscosComoEtiquetaCritica));
        var condiciones = await contexto.CondicionesMedicas
            .Include(c => c.TagsCriticos)
            .Where(c => c.Nombre == "Vegetariano" || c.Nombre == "Vegano")
            .ToListAsync();

        Assert.Equal(2, condiciones.Count);
        Assert.All(condiciones, condicion =>
            Assert.Contains(condicion.TagsCriticos, tag => tag.Nombre == "Mariscos"));
    }

    [Fact]
    public async Task PreferenciasGrupo_UsaEtiquetasDeParticipantesSeleccionados()
    {
        await using var contexto = DbContextEnMemoria.Crear(nameof(PreferenciasGrupo_UsaEtiquetasDeParticipantesSeleccionados));
        var vegetariano = await contexto.CondicionesMedicas.SingleAsync(c => c.Nombre == "Vegetariano");
        var alergia = await contexto.CondicionesMedicas.SingleAsync(c => c.Nombre == "Alergia al huevo");
        var incluido = new Usuario("incluido", "incluido@test.local", "Incluido", "Test", "incluido");
        incluido.CondicionesMedicas.Add(vegetariano);
        var excluido = new Usuario("excluido", "excluido@test.local", "Excluido", "Test", "excluido");
        excluido.CondicionesMedicas.Add(alergia);
        var grupo = new Grupo("Grupo", incluido.Id);
        grupo.Miembros.Add(new MiembroGrupo(grupo.Id, incluido.Id) { Usuario = incluido });
        grupo.Miembros.Add(new MiembroGrupo(grupo.Id, excluido.Id)
        {
            Usuario = excluido, ParticipaEnRecomendacion = false
        });
        contexto.Usuarios.AddRange(incluido, excluido);
        contexto.Grupos.Add(grupo);
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();

        var preferencias = await new MiembroGrupoRepositoryEF(contexto)
            .obtenerMiembrosActivosConSusPreferenciasYCondiciones(grupo.Id);

        Assert.Single(preferencias.ReglasCompatibilidad);
        Assert.Contains("mariscos", preferencias.ReglasCompatibilidad[0].TagsCriticos);
        Assert.DoesNotContain("Alergia al huevo", preferencias.CondicionesMedicas);
    }
}
