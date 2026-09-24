using GustosApp.Domain.Model;

namespace GustosApp.Application.Model;

public enum EstadoBusquedaRestaurantesGrupo
{
    Encontrados = 0,
    SinPreferencias = 1,
    SinOpciones = 2
}

public sealed record ResultadoBusquedaRestaurantesGrupo(
    EstadoBusquedaRestaurantesGrupo Estado,
    IReadOnlyList<Restaurante> Restaurantes);
