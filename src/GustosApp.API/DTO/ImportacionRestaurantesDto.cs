using GustosApp.Application.UseCases.RestauranteUseCases.Importacion;

namespace GustosApp.API.DTO;

public sealed class ImportacionRestaurantesDto
{
    public bool Confirmar { get; init; }
    public List<RestauranteImportacionEntrada> Restaurantes { get; init; } = new();
}
