namespace GustosApp.Application.UseCases.RestauranteUseCases.Importacion;

public sealed record RestauranteImportacionEntrada
{
    public string PlaceId { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Direccion { get; init; } = string.Empty;
    public double Latitud { get; init; }
    public double Longitud { get; init; }
    public string HorariosJson { get; init; } = "{}";
    public double? Rating { get; init; }
    public int? CantidadResenas { get; init; }
    public string? Categoria { get; init; }
    public DateTime? FechaDatosUtc { get; init; }
    public string? WebUrl { get; init; }
    public string PrimaryType { get; init; } = "restaurant";
    public string TypesJson { get; init; } = "[]";
    public string? ImagenUrl { get; init; }
    public bool PermitirTipoNoGastronomico { get; init; }
}
