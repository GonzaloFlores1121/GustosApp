namespace GustosApp.API.DTO;

public sealed class DescubrimientoRestaurantesDto
{
    public double Latitud { get; init; }
    public double Longitud { get; init; }
    public int RadioMetros { get; init; } = 1500;
    public int CantidadMaxima { get; init; } = 20;
}
