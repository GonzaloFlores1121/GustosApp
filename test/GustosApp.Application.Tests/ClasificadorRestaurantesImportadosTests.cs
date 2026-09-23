using FluentAssertions;
using GustosApp.Application.UseCases.RestauranteUseCases.Importacion;

namespace GustosApp.Application.Tests;

public sealed class ClasificadorRestaurantesImportadosTests
{
    private readonly ClasificadorRestaurantesImportados _clasificador = new();

    [Theory]
    [InlineData("pizza_restaurant", "Pizzería", "Pizza")]
    [InlineData("sushi_restaurant", "Restaurante de sushi", "Sushi")]
    [InlineData("ice_cream_shop", "Heladería", "Helado")]
    public void TipoEspecifico_DebeAsignarCategoriaYGustoDeAltaConfianza(
        string tipo,
        string categoria,
        string gusto)
    {
        var entrada = CrearEntrada("Local de prueba", tipo, $"[\"{tipo}\",\"restaurant\"]");

        var resultado = _clasificador.Clasificar(entrada);

        resultado.Categoria.Should().Be(categoria);
        resultado.GustosEstimados.Should().ContainSingle().Which.Should().Be(gusto);
    }

    [Theory]
    [InlineData("cafe", "Cafetería")]
    [InlineData("coffee_shop", "Cafetería")]
    [InlineData("sandwich_shop", "Sandwichería")]
    public void TipoAmplio_NoDebeInventarUnProductoEspecifico(string tipo, string categoria)
    {
        var entrada = CrearEntrada("Local de prueba", tipo, $"[\"{tipo}\",\"restaurant\"]");

        var resultado = _clasificador.Clasificar(entrada);

        resultado.Categoria.Should().Be(categoria);
        resultado.GustosEstimados.Should().BeEmpty();
    }

    [Fact]
    public void NombreInequivoco_DebeComplementarUnTipoGenerico()
    {
        var entrada = CrearEntrada("El Antojo - Parrilla y Asador", "restaurant", "[\"restaurant\"]");

        var resultado = _clasificador.Clasificar(entrada);

        resultado.Categoria.Should().Be("Parrilla");
        resultado.GustosEstimados.Should().Equal("Asado");
    }

    [Fact]
    public void RestauranteGenerico_NoDebeInventarGustos()
    {
        var entrada = CrearEntrada("El Antojo", "restaurant", "[\"restaurant\",\"food\"]");

        var resultado = _clasificador.Clasificar(entrada);

        resultado.Categoria.Should().Be("Restaurante");
        resultado.GustosEstimados.Should().BeEmpty();
    }

    [Fact]
    public void TipoSecundarioEspecifico_DebeUsarseCuandoElPrincipalEsGenerico()
    {
        var entrada = CrearEntrada(
            "Local de prueba",
            "restaurant",
            "[\"restaurant\",\"hamburger_restaurant\"]");

        var resultado = _clasificador.Clasificar(entrada);

        resultado.Categoria.Should().Be("Hamburguesería");
        resultado.GustosEstimados.Should().Equal("Hamburguesa");
    }

    private static RestauranteImportacionEntrada CrearEntrada(
        string nombre,
        string tipoPrincipal,
        string tiposJson) => new()
        {
            Nombre = nombre,
            PrimaryType = tipoPrincipal,
            TypesJson = tiposJson
        };
}
