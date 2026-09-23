using System.ComponentModel.DataAnnotations;

namespace GustosApp.Infraestructure.Services;

public sealed class ActualizacionExternaRestaurantesOptions
{
    public const string SectionName = "ActualizacionExternaRestaurantes";

    public bool Habilitada { get; init; }

    [Range(1, 10080)]
    public int IntervaloMinutos { get; init; } = 1440;

    [Range(1, 500)]
    public int TamanoLote { get; init; } = 50;
}
