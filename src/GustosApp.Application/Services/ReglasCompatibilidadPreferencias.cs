using GustosApp.Domain.Common;
using GustosApp.Domain.Model;

namespace GustosApp.Application.Services;

public static class ReglasCompatibilidadPreferencias
{
    public static List<ReglaCompatibilidadPreferencia> DesdeUsuario(Usuario usuario) =>
        usuario.Restricciones.Select(restriccion => new ReglaCompatibilidadPreferencia(
                restriccion.Id, true,
                restriccion.TagsProhibidos.Select(tag => tag.NombreNormalizado).ToArray()))
            .Concat(usuario.CondicionesMedicas.Select(condicion => new ReglaCompatibilidadPreferencia(
                condicion.Id, false,
                condicion.TagsCriticos.Select(tag => tag.NombreNormalizado).ToArray())))
            .ToList();
}
