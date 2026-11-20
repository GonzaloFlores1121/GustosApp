namespace GustosApp.Application.Interfaces;

public sealed record RestauranteAdministracion(Guid Id, string Nombre, string Direccion, string? PlaceId,
    Guid? DuenoId, bool Desactivado, int VersionDisponibilidad, string? MotivoDisponibilidad,
    DateTime? FechaCambioDisponibilidadUtc);
public sealed record PaginaAdministracionRestaurantes(IReadOnlyList<RestauranteAdministracion> Elementos,
    int Total, int Pagina, int TamanoPagina);
public sealed record CambioDisponibilidadRestaurante(bool Desactivado, int VersionEsperada, string Motivo);
public sealed record CambioArchivoSolicitud(bool Archivada, int VersionEsperada);

public interface IAdministracionRestaurantes
{
    Task<PaginaAdministracionRestaurantes> BuscarAsync(string? texto, bool? desactivado, int pagina, int tamanoPagina, CancellationToken ct);
    Task<RestauranteAdministracion> CambiarDisponibilidadAsync(Guid id, CambioDisponibilidadRestaurante cambio, string actorUid, CancellationToken ct);
    Task CambiarArchivoAsync(Guid id, CambioArchivoSolicitud cambio, string actorUid, CancellationToken ct);
}
