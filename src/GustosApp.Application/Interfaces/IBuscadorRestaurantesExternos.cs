using GustosApp.Application.UseCases.RestauranteUseCases.Importacion;

namespace GustosApp.Application.Interfaces;

public interface IBuscadorRestaurantesExternos
{
    Task<IReadOnlyCollection<RestauranteImportacionEntrada>> BuscarCercanosAsync(
        SolicitudDescubrimientoRestaurantes solicitud,
        CancellationToken ct = default);
}
