using GustosApp.Domain.Common;
using GustosApp.Domain.Model;

namespace GustosApp.Application.Services;

/// <summary>Evalúa opciones conocidas sin presumir que sus etiquetas describen todos sus ingredientes.</summary>
public class EvaluadorCompatibilidadRestaurante
{
    private readonly TimeProvider _reloj;

    public EvaluadorCompatibilidadRestaurante(TimeProvider reloj) => _reloj = reloj;

    public EvaluadorCompatibilidadRestaurante() : this(TimeProvider.System) { }

    public IReadOnlyCollection<Gusto> ObtenerOpcionesSinConflictoConocido(
        UsuarioPreferencias preferencias, Restaurante restaurante)
    {
        var tagsCriticos = preferencias.ReglasCompatibilidad
            .SelectMany(regla => regla.TagsCriticos)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return restaurante.GustosQueSirve
            .Where(gusto => !gusto.Tags.Any(tag => tagsCriticos.Contains(tag.NombreNormalizado)))
            .ToList();
    }

    public NivelCompatibilidadRestaurante Evaluar(UsuarioPreferencias preferencias, Restaurante restaurante)
    {
        if (preferencias.Restricciones.Count == 0 && preferencias.CondicionesMedicas.Count == 0 &&
            preferencias.ReglasCompatibilidad.Count == 0)
            return NivelCompatibilidadRestaurante.Estimada;

        var reglas = preferencias.ReglasCompatibilidad
            .DistinctBy(regla => (regla.Id, regla.EsRestriccion))
            .ToList();
        if (reglas.Count == 0)
            return NivelCompatibilidadRestaurante.Desconocida;

        var idsDeclarados = restaurante.RestriccionesQueRespeta.Select(r => r.Id).ToHashSet();
        var sinDeclaracion = reglas.Where(regla => !regla.EsRestriccion ||
            restaurante.ObtenerEstadoDeclaracion(regla.Id, _reloj.GetUtcNow().UtcDateTime) !=
            EstadoVigenciaInformacion.Vigente).ToList();

        if (sinDeclaracion.Count == 0 && reglas.Count ==
            preferencias.Restricciones.Count + preferencias.CondicionesMedicas.Count)
            return NivelCompatibilidadRestaurante.Estimada;

        // Solo se excluye cuando todas las opciones registradas tienen un conflicto
        // conocido. Una opción sin etiqueta crítica es potencial, nunca confirmada.
        var reglasConConflicto = sinDeclaracion
            .Where(regla => regla.TagsCriticos.Count > 0 &&
                !(regla.EsRestriccion && idsDeclarados.Contains(regla.Id)))
            .Select(regla => regla.TagsCriticos.ToHashSet(StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (reglas.Count == preferencias.Restricciones.Count + preferencias.CondicionesMedicas.Count &&
            restaurante.GustosQueSirve.Count > 0 && reglasConConflicto.Count > 0 &&
            restaurante.GustosQueSirve.All(gusto =>
                reglasConConflicto.Any(tagsCriticos => gusto.Tags.Any(tag =>
                    tagsCriticos.Contains(tag.NombreNormalizado)))))
            return NivelCompatibilidadRestaurante.Incompatible;

        return NivelCompatibilidadRestaurante.Desconocida;
    }
}
