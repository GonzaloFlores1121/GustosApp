using System.Text.Json;
using System.Text.RegularExpressions;
using GustosApp.Domain.Model;

namespace GustosApp.Application.Services;

/// <summary>Encuentra pistas explícitas en nombres de la oferta, sin inferir ingredientes ausentes.</summary>
public sealed class SugeridorRestriccionesOferta
{
    private static readonly (string Restriccion, string Marca)[] Marcas =
    [
        ("Sin gluten", "sin gluten"),
        ("Sin gluten", "libre de gluten"),
        ("Sin lactosa", "sin lactosa"),
        ("Sin lactosa", "libre de lactosa")
    ];

    private static readonly Regex NombreExplicito = new(
        @"^(?<producto>[\p{L}\p{N}][\p{L}\p{N}\s,/-]{1,100}?)\s+(?<marca>sin gluten|libre de gluten|sin lactosa|libre de lactosa)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex ContextoIncierto = new(
        @"\b(no|nunca|consultar|opcional|estilo|tipo|sabor|contiene|puede contener|con gluten|con lactosa|disponibilidad)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public IReadOnlyList<SugerenciaRestriccionOferta> Sugerir(
        IEnumerable<Gusto> gustosSeleccionados, string? menuPublicadoJson,
        IEnumerable<Restriccion> catalogo, IEnumerable<Guid> declaracionesGuardadas)
    {
        var disponibles = catalogo
            .Where(restriccion => restriccion.Id != Guid.Empty && !string.IsNullOrWhiteSpace(restriccion.Nombre))
            .GroupBy(restriccion => restriccion.Nombre.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.First(), StringComparer.OrdinalIgnoreCase);
        var guardadas = declaracionesGuardadas.ToHashSet();
        var resultado = new Dictionary<Guid, SugerenciaRestriccionOferta>();

        foreach (var gusto in gustosSeleccionados)
            Agregar(gusto.Nombre, "Gusto seleccionado", gusto.Id, gusto.Tags, disponibles, guardadas, resultado);
        foreach (var nombre in LeerNombresMenuPublicado(menuPublicadoJson))
            Agregar(nombre, "Menú publicado", null, null, disponibles, guardadas, resultado);

        return resultado.Values.OrderBy(sugerencia => sugerencia.Nombre).ToArray();
    }

    private static void Agregar(string? nombreFuente, string tipoFuente, Guid? fuenteGustoId, ICollection<Tag>? tags,
        IReadOnlyDictionary<string, Restriccion> catalogo, HashSet<Guid> guardadas,
        IDictionary<Guid, SugerenciaRestriccionOferta> resultado)
    {
        var nombre = nombreFuente?.Trim();
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 120) return;
        var coincidencia = NombreExplicito.Match(nombre);
        if (!coincidencia.Success || ContextoIncierto.IsMatch(coincidencia.Groups["producto"].Value)) return;
        var marca = coincidencia.Groups["marca"].Value;
        var nombreRestriccion = Marcas.FirstOrDefault(item =>
            string.Equals(item.Marca, marca, StringComparison.OrdinalIgnoreCase)).Restriccion;
        if (nombreRestriccion is null || !catalogo.TryGetValue(nombreRestriccion, out var restriccion) ||
            guardadas.Contains(restriccion.Id) || resultado.ContainsKey(restriccion.Id)) return;

        // Los tags del gusto indican posibles conflictos, nunca evidencia de seguridad.
        if (tags is not null && tags.Any(tag => restriccion.TagsProhibidos.Any(prohibido =>
                string.Equals(tag.NombreNormalizado, prohibido.NombreNormalizado, StringComparison.OrdinalIgnoreCase))))
            return;

        resultado.Add(restriccion.Id, new SugerenciaRestriccionOferta(
            restriccion.Id, restriccion.Nombre, tipoFuente, nombre, fuenteGustoId,
            $"{tipoFuente}: «{nombre}». Revisá ingredientes, preparación y disponibilidad antes de declarar opciones."));
    }

    private static IEnumerable<string> LeerNombresMenuPublicado(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 1_000_000) yield break;
        JsonDocument documento;
        try { documento = JsonDocument.Parse(json); }
        catch (JsonException) { yield break; }
        using (documento)
        {
            if (documento.RootElement.ValueKind != JsonValueKind.Object ||
                !documento.RootElement.TryGetProperty("categorias", out var categorias) ||
                categorias.ValueKind != JsonValueKind.Array) yield break;
            foreach (var categoria in categorias.EnumerateArray().Take(100))
            {
                if (categoria.ValueKind != JsonValueKind.Object ||
                    !categoria.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var item in items.EnumerateArray().Take(200))
                    if (item.ValueKind == JsonValueKind.Object &&
                        item.TryGetProperty("nombre", out var nombre) && nombre.ValueKind == JsonValueKind.String)
                        yield return nombre.GetString() ?? string.Empty;
            }
        }
    }
}

/// <summary>Pista para revisión humana, no una declaración ni certificación alimentaria.</summary>
public sealed record SugerenciaRestriccionOferta(
    Guid Id, string Nombre, string TipoFuente, string Fuente, Guid? FuenteGustoId, string Motivo);
