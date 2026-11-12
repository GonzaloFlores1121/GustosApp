namespace GustosApp.Application.Interfaces;

public sealed record AutorFotoGoogle(string Nombre, string? PerfilUrl, string? AvatarUrl);
public sealed record FotoGoogleResultado(string ContenidoBase64, string TipoContenido, IReadOnlyList<AutorFotoGoogle> Autores, string? FuenteUrl);
public interface IFotoGoogleService
{
    Task<FotoGoogleResultado?> ObtenerAsync(Guid restauranteId, string referencia, CancellationToken ct);
}
