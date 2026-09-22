namespace GustosApp.Application.UseCases.RestauranteUseCases.Importacion;

public sealed record SolicitudDescubrimientoRestaurantes(
    double Latitud,
    double Longitud,
    int RadioMetros = 1500,
    int CantidadMaxima = 20);
