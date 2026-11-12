using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Services;
using GustosApp.Domain.Interfaces;

namespace GustosApp.Application.UseCases.RestauranteUseCases;

/// <summary>Lee la oferta de un propietario y devuelve pistas sin modificar relaciones ni vigencia.</summary>
public sealed class SugerirRestriccionesOfertaUseCase(
    IRestauranteRepository restaurantes, IGustoRepository gustos,
    IRestriccionRepository restricciones, IRestauranteMenuRepository menus,
    SugeridorRestriccionesOferta sugeridor)
{
    public async Task<ResultadoSugerenciasRestriccionesOferta> HandleAsync(
        Guid restauranteId, Guid propietarioId, IReadOnlyCollection<Guid> gustoIds,
        CancellationToken ct = default)
    {
        var restaurante = await restaurantes.GetByIdAsync(restauranteId, ct)
            ?? throw new KeyNotFoundException("Restaurante no encontrado.");
        if (restaurante.DuenoId != propietarioId)
            throw new AccesoProhibidoException("No tenés permisos para revisar la oferta de este restaurante.");

        if (gustoIds.Count > 200 || gustoIds.Any(id => id == Guid.Empty))
            throw new ArgumentException("La selección de gustos no es válida.");
        var ids = gustoIds.Distinct().ToList();
        var seleccionados = ids.Count == 0 ? [] : await gustos.GetByIdsAsync(ids, ct);
        if (seleccionados.Select(gusto => gusto.Id).Distinct().Count() != ids.Count)
            throw new ArgumentException("La selección incluye gustos que no existen en el catálogo.");

        var catalogo = await restricciones.GetAllAsync(ct);
        var menu = restaurante.MenuProcesado == true
            ? await menus.GetByRestauranteIdAsync(restauranteId, ct)
            : null;
        var sugerencias = sugeridor.Sugerir(seleccionados, menu?.Json, catalogo,
            restaurante.RestriccionesQueRespeta.Select(restriccion => restriccion.Id));
        return new ResultadoSugerenciasRestriccionesOferta(
            restaurante.VersionOferta, menu?.Version, sugerencias);
    }
}

public sealed record ResultadoSugerenciasRestriccionesOferta(
    int VersionOferta, int? VersionMenu,
    IReadOnlyList<SugerenciaRestriccionOferta> Sugerencias);
