namespace GustosApp.Application.UseCases.RestauranteUseCases.Importacion;

public sealed record ResultadoDescubrimientoRestaurantes(
    IReadOnlyCollection<RestauranteImportacionEntrada> Restaurantes,
    ResultadoImportacionRestaurantes VistaPrevia);
