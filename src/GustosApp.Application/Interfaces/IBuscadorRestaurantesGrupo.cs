using GustosApp.Application.Model;

namespace GustosApp.Application.Interfaces;

public interface IBuscadorRestaurantesGrupo
{
    Task<ResultadoBusquedaRestaurantesGrupo> BuscarAsync(
        string firebaseUid,
        Guid grupoId,
        double? latitud,
        double? longitud,
        int? radioMetros,
        int cantidadMaxima,
        CancellationToken cancellationToken = default);
}
