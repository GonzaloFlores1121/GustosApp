using System.Globalization;
using System.Text;
using System.Text.Json;

namespace GustosApp.Application.UseCases.RestauranteUseCases.Importacion;

public sealed record ClasificacionRestauranteImportado(
    string Categoria,
    IReadOnlyCollection<string> GustosEstimados);

public sealed class ClasificadorRestaurantesImportados
{
    private static readonly IReadOnlyDictionary<string, (string Categoria, string[] Gustos)> ReglasPorTipo =
        new Dictionary<string, (string, string[])>(StringComparer.OrdinalIgnoreCase)
        {
            ["pizza_restaurant"] = ("Pizzería", ["Pizza"]),
            ["hamburger_restaurant"] = ("Hamburguesería", ["Hamburguesa"]),
            ["sushi_restaurant"] = ("Restaurante de sushi", ["Sushi"]),
            ["ramen_restaurant"] = ("Restaurante de ramen", ["Ramen japonés"]),
            ["mexican_restaurant"] = ("Restaurante mexicano", ["Tacos"]),
            ["italian_restaurant"] = ("Restaurante italiano", []),
            ["seafood_restaurant"] = ("Marisquería", []),
            ["steak_house"] = ("Parrilla", []),
            ["barbecue_restaurant"] = ("Parrilla", []),
            ["ice_cream_shop"] = ("Heladería", ["Helado"]),
            ["cafe"] = ("Cafetería", ["Café con leche"]),
            ["coffee_shop"] = ("Cafetería", ["Café con leche"]),
            ["bakery"] = ("Panadería", []),
            ["sandwich_shop"] = ("Sandwichería", ["Sándwich de jamón y queso"]),
            ["vegan_restaurant"] = ("Restaurante vegano", []),
            ["vegetarian_restaurant"] = ("Restaurante vegetariano", []),
            ["fast_food_restaurant"] = ("Comida rápida", []),
            ["breakfast_restaurant"] = ("Desayunos", []),
            ["food_court"] = ("Patio de comidas", []),
            ["bar"] = ("Bar", []),
            ["pub"] = ("Bar", []),
            ["restaurant"] = ("Restaurante", [])
        };

    private static readonly (string[] Palabras, string Categoria, string Gusto)[] ReglasPorNombre =
    [
        (["pizzeria", "pizza"], "Pizzería", "Pizza"),
        (["sushi"], "Restaurante de sushi", "Sushi"),
        (["ramen"], "Restaurante de ramen", "Ramen japonés"),
        (["heladeria", "helado"], "Heladería", "Helado"),
        (["hamburgues"], "Hamburguesería", "Hamburguesa"),
        (["parrilla", "asador", "asado"], "Parrilla", "Asado"),
        (["empanada"], "Casa de empanadas", "Empanadas"),
        (["ceviche"], "Cevichería", "Ceviche"),
        (["kebab"], "Restaurante de kebab", "Kebab"),
        (["taco"], "Taquería", "Tacos")
    ];

    public ClasificacionRestauranteImportado Clasificar(RestauranteImportacionEntrada entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        var tipos = ObtenerTipos(entrada);
        var reglaTipo = tipos
            .OrderBy(tipo => tipo.Equals("restaurant", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .Select(tipo => ReglasPorTipo.TryGetValue(tipo, out var regla) ? regla : ((string Categoria, string[] Gustos)?)null)
            .FirstOrDefault(regla => regla.HasValue);
        var categoria = reglaTipo?.Categoria ?? "Restaurante";
        var gustos = new HashSet<string>(reglaTipo?.Gustos ?? [], StringComparer.OrdinalIgnoreCase);
        var nombreNormalizado = NormalizarTexto(entrada.Nombre);

        foreach (var regla in ReglasPorNombre)
        {
            if (!regla.Palabras.Any(nombreNormalizado.Contains))
                continue;

            gustos.Add(regla.Gusto);
            if (categoria == "Restaurante")
                categoria = regla.Categoria;
        }

        return new ClasificacionRestauranteImportado(categoria, gustos.OrderBy(gusto => gusto).ToArray());
    }

    private static IReadOnlyCollection<string> ObtenerTipos(RestauranteImportacionEntrada entrada)
    {
        var tipos = new List<string>();
        if (!string.IsNullOrWhiteSpace(entrada.PrimaryType))
            tipos.Add(entrada.PrimaryType.Trim());

        try
        {
            using var documento = JsonDocument.Parse(entrada.TypesJson);
            tipos.AddRange(documento.RootElement
                .EnumerateArray()
                .Where(elemento => elemento.ValueKind == JsonValueKind.String)
                .Select(elemento => elemento.GetString())
                .OfType<string>());
        }
        catch (JsonException)
        {
            // La validación del caso de uso informará el JSON inválido.
        }

        return tipos.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string NormalizarTexto(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var caracteres = descompuesto.Where(caracter =>
            CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark);
        return new string(caracteres.ToArray()).Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
