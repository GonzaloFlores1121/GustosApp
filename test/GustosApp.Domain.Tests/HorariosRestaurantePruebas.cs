using System.Text.Json;
using GustosApp.Domain.Common;

namespace GustosApp.Domain.Tests;

public sealed class HorariosRestaurantePruebas
{
    public static IEnumerable<object?[]> Casos()
    {
        using var documento = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Datos", "horarios.json")));
        return documento.RootElement.EnumerateArray().Select(c => new object?[] {
            c.GetProperty("nombre").GetString(), c.GetProperty("instante").GetString(),
            c.GetProperty("horariosJson").GetString(), c.GetProperty("esperado").GetString()
        }).ToArray();
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void Evaluar_SemanaArgentinaYDatosIncompletos_UsaContratoCompartido(
        string nombre, string instante, string? horariosJson, string esperado)
    {
        Assert.False(string.IsNullOrWhiteSpace(nombre));
        Assert.Equal(Enum.Parse<EstadoAperturaRestaurante>(esperado),
            HorariosRestaurante.Evaluar(horariosJson, DateTimeOffset.Parse(instante)));
    }

    [Fact]
    public void Evaluar_JsonMalformado_DevuelveDesconocido() =>
        Assert.Equal(EstadoAperturaRestaurante.Desconocido, HorariosRestaurante.Evaluar("[", DateTimeOffset.UtcNow));
}
