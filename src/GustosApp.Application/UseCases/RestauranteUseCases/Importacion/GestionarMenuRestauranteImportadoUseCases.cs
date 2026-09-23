using System.Globalization;
using System.Text;
using System.Text.Json;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Microsoft.Extensions.Logging;

namespace GustosApp.Application.UseCases.RestauranteUseCases.Importacion;

public sealed record GustoSugeridoMenu(Guid Id, string Nombre);

public sealed record ResultadoAnalisisMenuRestaurante(
    Guid RestauranteId,
    string Restaurante,
    string TextoExtraido,
    string CategoriaSugerida,
    IReadOnlyCollection<GustoSugeridoMenu> GustosSugeridos,
    IReadOnlyCollection<GustoSugeridoMenu> CatalogoGustos,
    bool AnalizadoConIa,
    string DetalleAnalisis,
    string Advertencia);

public sealed record RestauranteParaGestionMenu(
    Guid Id,
    string Nombre,
    string Direccion,
    string? Categoria,
    string EstadoMenu,
    IReadOnlyCollection<GustoSugeridoMenu> Gustos);

public sealed record DetalleGestionRestaurante(
    Guid Id,
    string Nombre,
    string Direccion,
    string? Categoria,
    string EstadoMenu,
    IReadOnlyCollection<GustoSugeridoMenu> Gustos,
    IReadOnlyCollection<GustoSugeridoMenu> CatalogoGustos);

public sealed record RestaurantePendienteClasificacion(
    Guid Id,
    string Nombre,
    string Direccion,
    string? Categoria,
    string EstadoMenu,
    IReadOnlyCollection<string> Motivos,
    IReadOnlyCollection<GustoSugeridoMenu> Gustos);

public sealed class AnalizarMenuRestauranteImportadoUseCase
{
    private static readonly IReadOnlyDictionary<string, string[]> EquivalenciasPorGusto =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["cafe con leche"] = ["latte", "capuccino", "cappuccino", "flat white", "macchiato", "mocaccino"],
            ["cafe"] = ["espresso", "ristretto", "americano", "cafe"],
            ["pasteleria"] = ["croissant", "pain au chocolat", "roll de canela", "cookie", "brownie"],
            ["te"] = ["matcha", "chai", "te negro", "te verde"]
        };

    private readonly IRestauranteRepository _restaurantes;
    private readonly IGustoRepository _gustos;
    private readonly IOcrService _ocr;
    private readonly IRecomendacionAIService _ia;
    private readonly ILogger<AnalizarMenuRestauranteImportadoUseCase> _logger;

    public AnalizarMenuRestauranteImportadoUseCase(
        IRestauranteRepository restaurantes,
        IGustoRepository gustos,
        IOcrService ocr,
        IRecomendacionAIService ia,
        ILogger<AnalizarMenuRestauranteImportadoUseCase> logger)
    {
        _restaurantes = restaurantes;
        _gustos = gustos;
        _ocr = ocr;
        _ia = ia;
        _logger = logger;
    }

    public async Task<ResultadoAnalisisMenuRestaurante> HandleAsync(
        Guid restauranteId,
        string? textoIngresado,
        IReadOnlyCollection<Stream> imagenes,
        CancellationToken ct = default)
    {
        var restaurante = await ObtenerImportadoAsync(restauranteId, ct);
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(textoIngresado))
            partes.Add(textoIngresado.Trim());
        if (imagenes.Count > 0)
        {
            var textoOcr = await _ocr.ReconocerTextoAsync(imagenes, "spa+eng", ct);
            if (!string.IsNullOrWhiteSpace(textoOcr))
                partes.Add(textoOcr.Trim());
        }

        var texto = string.Join(Environment.NewLine, partes);
        if (string.IsNullOrWhiteSpace(texto))
            throw new ArgumentException("Ingresá el texto del menú o adjuntá al menos una imagen legible.");
        if (texto.Length > 30_000)
            throw new ArgumentException("El texto del menú no puede superar los 30.000 caracteres.");

        var catalogo = await _gustos.GetAllAsync(ct);
        var sugerenciasLocales = ClasificarLocalmente(texto, catalogo);
        var categoria = restaurante.Categoria ?? "Restaurante";
        var analizadoConIa = false;
        var detalleAnalisis = "Gemini no estuvo disponible; las sugerencias se obtuvieron mediante coincidencias locales.";

        try
        {
            var respuesta = await _ia.GenerarRecomendacion(CrearPrompt(texto, catalogo));
            if (IntentarLeerRespuesta(respuesta, catalogo, out var categoriaIa, out var gustosIa))
            {
                categoria = categoriaIa;
                sugerenciasLocales.UnionWith(gustosIa);
                analizadoConIa = true;
                detalleAnalisis = "Gemini complementó las coincidencias locales usando únicamente gustos existentes del catálogo.";
            }
            else if (!string.IsNullOrWhiteSpace(respuesta) && !respuesta.StartsWith("Error ", StringComparison.OrdinalIgnoreCase))
                detalleAnalisis = "Gemini respondió, pero el formato no se pudo interpretar; se conservaron las coincidencias locales.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Gemini no pudo complementar el análisis del menú del restaurante {RestauranteId}",
                restauranteId);
        }

        return new ResultadoAnalisisMenuRestaurante(
            restaurante.Id,
            restaurante.Nombre,
            texto,
            categoria,
            sugerenciasLocales.OrderBy(gusto => gusto.Nombre)
                .Select(gusto => new GustoSugeridoMenu(gusto.Id, gusto.Nombre)).ToArray(),
            catalogo.OrderBy(gusto => gusto.Nombre)
                .Select(gusto => new GustoSugeridoMenu(gusto.Id, gusto.Nombre)).ToArray(),
            analizadoConIa,
            detalleAnalisis,
            "Las sugerencias deben revisarse antes de guardarlas. El menú no demuestra ausencia de contaminación cruzada ni compatibilidad médica.");
    }

    private async Task<Restaurante> ObtenerImportadoAsync(Guid restauranteId, CancellationToken ct)
    {
        var restaurante = await _restaurantes.GetByIdAsync(restauranteId, ct)
            ?? throw new KeyNotFoundException("Restaurante no encontrado.");
        if (restaurante.DuenoId.HasValue || !string.IsNullOrWhiteSpace(restaurante.PropietarioUid))
            throw new InvalidOperationException("Este flujo es sólo para restaurantes importados sin propietario.");
        return restaurante;
    }

    private static HashSet<Gusto> ClasificarLocalmente(string texto, IReadOnlyCollection<Gusto> catalogo)
    {
        var normalizado = Normalizar(texto);
        return catalogo.Where(gusto =>
            {
                if (string.IsNullOrWhiteSpace(gusto.Nombre)) return false;
                var nombre = Normalizar(gusto.Nombre);
                if (normalizado.Contains(nombre)) return true;
                return EquivalenciasPorGusto.TryGetValue(nombre, out var equivalencias)
                    && equivalencias.Any(normalizado.Contains);
            })
            .ToHashSet();
    }

    private static string CrearPrompt(string texto, IReadOnlyCollection<Gusto> catalogo) =>
        $$"""
        Analizá este menú de restaurante. Respondé solamente JSON válido con esta forma:
        {"categoria":"categoría general breve en español","gustos":["nombre exacto del catálogo"]}
        Sólo podés elegir gustos de este catálogo: {{string.Join(", ", catalogo.Select(g => g.Nombre))}}.
        No infieras restricciones, alergias, seguridad alimentaria ni platos que no aparezcan en el menú.
        Menú:
        {{texto}}
        """;

    private static bool IntentarLeerRespuesta(
        string respuesta,
        IReadOnlyCollection<Gusto> catalogo,
        out string categoria,
        out IReadOnlyCollection<Gusto> gustos)
    {
        categoria = string.Empty;
        gustos = [];
        if (string.IsNullOrWhiteSpace(respuesta) || respuesta.StartsWith("Error ", StringComparison.OrdinalIgnoreCase))
            return false;

        var inicio = respuesta.IndexOf('{');
        var fin = respuesta.LastIndexOf('}');
        if (inicio < 0 || fin <= inicio) return false;

        try
        {
            using var documento = JsonDocument.Parse(respuesta[inicio..(fin + 1)]);
            categoria = documento.RootElement.GetProperty("categoria").GetString()?.Trim() ?? string.Empty;
            if (categoria.Length is 0 or > 100) return false;
            var porNombre = catalogo.ToDictionary(g => g.Nombre, StringComparer.OrdinalIgnoreCase);
            gustos = documento.RootElement.GetProperty("gustos").EnumerateArray()
                .Select(item => item.GetString())
                .Where(nombre => nombre is not null && porNombre.ContainsKey(nombre))
                .Select(nombre => porNombre[nombre!])
                .DistinctBy(gusto => gusto.Id)
                .ToArray();
            return true;
        }
        catch (JsonException) { return false; }
        catch (KeyNotFoundException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    private static string Normalizar(string valor)
    {
        var descompuesto = valor.Normalize(NormalizationForm.FormD);
        return new string(descompuesto.Where(c =>
                CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}

public sealed class ObtenerPendientesClasificacionRestauranteUseCase
{
    public const int CantidadMaxima = 100;
    private readonly IRestauranteRepository _restaurantes;

    public ObtenerPendientesClasificacionRestauranteUseCase(IRestauranteRepository restaurantes) =>
        _restaurantes = restaurantes;

    public async Task<IReadOnlyCollection<RestaurantePendienteClasificacion>> HandleAsync(
        CancellationToken ct = default)
    {
        var restaurantes = await _restaurantes.ObtenerPendientesClasificacionAsync(CantidadMaxima, ct);
        return restaurantes.Select(restaurante => new RestaurantePendienteClasificacion(
                restaurante.Id,
                restaurante.Nombre,
                restaurante.Direccion,
                restaurante.Categoria,
                ObtenerEstadoMenu(restaurante),
                ObtenerMotivos(restaurante),
                restaurante.GustosQueSirve.OrderBy(gusto => gusto.Nombre)
                    .Select(gusto => new GustoSugeridoMenu(gusto.Id, gusto.Nombre)).ToArray()))
            .ToArray();
    }

    private static IReadOnlyCollection<string> ObtenerMotivos(Restaurante restaurante)
    {
        var motivos = new List<string>();
        if (!string.IsNullOrWhiteSpace(restaurante.MenuError)) motivos.Add("Error al procesar menú");
        else if (restaurante.MenuProcesado != true) motivos.Add("Sin menú procesado");
        if (restaurante.GustosQueSirve.Count == 0) motivos.Add("Sin gustos");
        if (restaurante.OrigenDatosCompatibilidad == OrigenDatosCompatibilidadRestaurante.GooglePlaces)
            motivos.Add("Clasificación automática");
        return motivos;
    }

    internal static string ObtenerEstadoMenu(Restaurante restaurante) =>
        !string.IsNullOrWhiteSpace(restaurante.MenuError)
            ? "Error de procesamiento"
            : restaurante.MenuProcesado == true ? "Procesado" : "Sin procesar";
}

public sealed class ObtenerDetalleGestionRestauranteUseCase
{
    private readonly IRestauranteRepository _restaurantes;
    private readonly IGustoRepository _gustos;

    public ObtenerDetalleGestionRestauranteUseCase(
        IRestauranteRepository restaurantes,
        IGustoRepository gustos)
    {
        _restaurantes = restaurantes;
        _gustos = gustos;
    }

    public async Task<DetalleGestionRestaurante> HandleAsync(Guid restauranteId, CancellationToken ct = default)
    {
        var restaurante = await ObtenerRestauranteImportadoAsync(_restaurantes, restauranteId, ct);
        var catalogo = await _gustos.GetAllAsync(ct);

        return new DetalleGestionRestaurante(
            restaurante.Id,
            restaurante.Nombre,
            restaurante.Direccion,
            restaurante.Categoria,
            ObtenerPendientesClasificacionRestauranteUseCase.ObtenerEstadoMenu(restaurante),
            restaurante.GustosQueSirve.OrderBy(gusto => gusto.Nombre)
                .Select(gusto => new GustoSugeridoMenu(gusto.Id, gusto.Nombre)).ToArray(),
            catalogo.OrderBy(gusto => gusto.Nombre)
                .Select(gusto => new GustoSugeridoMenu(gusto.Id, gusto.Nombre)).ToArray());
    }

    internal static async Task<Restaurante> ObtenerRestauranteImportadoAsync(
        IRestauranteRepository restaurantes,
        Guid restauranteId,
        CancellationToken ct)
    {
        var restaurante = await restaurantes.GetByIdAsync(restauranteId, ct)
            ?? throw new KeyNotFoundException("Restaurante no encontrado.");
        if (restaurante.DuenoId.HasValue || !string.IsNullOrWhiteSpace(restaurante.PropietarioUid))
            throw new InvalidOperationException("Este flujo es sólo para restaurantes importados sin propietario.");
        return restaurante;
    }
}

public sealed class GuardarClasificacionRestauranteImportadoUseCase
{
    private readonly IRestauranteRepository _restaurantes;
    private readonly IGustoRepository _gustos;
    private readonly TimeProvider _reloj;

    public GuardarClasificacionRestauranteImportadoUseCase(
        IRestauranteRepository restaurantes,
        IGustoRepository gustos,
        TimeProvider reloj)
    {
        _restaurantes = restaurantes;
        _gustos = gustos;
        _reloj = reloj;
    }

    public async Task HandleAsync(
        Guid restauranteId,
        string categoria,
        IReadOnlyCollection<Guid> gustoIds,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(categoria) || categoria.Trim().Length > 100)
            throw new ArgumentException("La categoría debe tener entre 1 y 100 caracteres.");

        var restaurante = await ObtenerDetalleGestionRestauranteUseCase.ObtenerRestauranteImportadoAsync(
            _restaurantes, restauranteId, ct);
        var idsUnicos = gustoIds.Distinct().ToList();
        var gustos = await _gustos.GetByIdsAsync(idsUnicos, ct);
        if (gustos.Count != idsUnicos.Count)
            throw new ArgumentException("Uno o más gustos seleccionados no existen.");

        var ahora = _reloj.GetUtcNow().UtcDateTime;
        restaurante.Categoria = categoria.Trim();
        restaurante.SetGustos(gustos);
        restaurante.ActualizadoUtc = ahora;
        restaurante.RegistrarDatosCompatibilidadEstimados(
            OrigenDatosCompatibilidadRestaurante.Administracion, ahora);
        await _restaurantes.UpdateAsync(restaurante, ct);
        await _restaurantes.SaveChangesAsync(ct);
    }
}

public sealed class ConfirmarMenuRestauranteImportadoUseCase
{
    private readonly IRestauranteRepository _restaurantes;
    private readonly IGustoRepository _gustos;
    private readonly IRestauranteMenuRepository _menus;
    private readonly IMenuParser _parser;
    private readonly TimeProvider _reloj;

    public ConfirmarMenuRestauranteImportadoUseCase(
        IRestauranteRepository restaurantes,
        IGustoRepository gustos,
        IRestauranteMenuRepository menus,
        IMenuParser parser,
        TimeProvider reloj)
    {
        _restaurantes = restaurantes;
        _gustos = gustos;
        _menus = menus;
        _parser = parser;
        _reloj = reloj;
    }

    public async Task HandleAsync(Guid restauranteId, string texto, string categoria, List<Guid> gustoIds, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(texto)) throw new ArgumentException("El texto del menú es obligatorio.");
        if (texto.Length > 30_000) throw new ArgumentException("El texto del menú no puede superar los 30.000 caracteres.");
        if (string.IsNullOrWhiteSpace(categoria) || categoria.Trim().Length > 100)
            throw new ArgumentException("La categoría debe tener entre 1 y 100 caracteres.");

        var restaurante = await ObtenerDetalleGestionRestauranteUseCase.ObtenerRestauranteImportadoAsync(
            _restaurantes, restauranteId, ct);

        var gustos = await _gustos.GetByIdsAsync(gustoIds.Distinct().ToList(), ct);
        if (gustos.Count != gustoIds.Distinct().Count())
            throw new ArgumentException("Uno o más gustos seleccionados no existen.");

        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var json = await _parser.ParsearAsync(texto.Trim(), "ARS", ct);
        var menu = await _menus.GetByRestauranteIdAsync(restauranteId, ct);
        if (menu is null)
        {
            await _menus.AddAsync(new RestauranteMenu
            {
                RestauranteId = restauranteId,
                Json = json,
                FechaActualizacionUtc = ahora
            }, ct);
        }
        else
        {
            menu.Json = json;
            menu.Version++;
            menu.FechaActualizacionUtc = ahora;
            await _menus.UpdateAsync(menu, ct);
        }

        restaurante.Categoria = categoria.Trim();
        restaurante.SetGustos(gustos);
        restaurante.MenuProcesado = true;
        restaurante.MenuError = null;
        restaurante.ActualizadoUtc = ahora;
        restaurante.RegistrarDatosCompatibilidadEstimados(OrigenDatosCompatibilidadRestaurante.Administracion, ahora);
        await _restaurantes.UpdateAsync(restaurante, ct);
        await _restaurantes.SaveChangesAsync(ct);
    }
}

public sealed class BuscarRestaurantesParaGestionMenuUseCase
{
    private readonly IRestauranteRepository _restaurantes;
    public BuscarRestaurantesParaGestionMenuUseCase(IRestauranteRepository restaurantes) => _restaurantes = restaurantes;

    public async Task<IReadOnlyCollection<RestauranteParaGestionMenu>> HandleAsync(string texto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length < 2)
            throw new ArgumentException("Ingresá al menos dos caracteres para buscar.");
        var encontrados = await _restaurantes.BuscarPorTextoAsync(texto.Trim(), ct);
        return encontrados.Where(r => !r.DuenoId.HasValue && string.IsNullOrWhiteSpace(r.PropietarioUid))
            .Take(20)
            .Select(r => new RestauranteParaGestionMenu(r.Id, r.Nombre, r.Direccion, r.Categoria,
                ObtenerPendientesClasificacionRestauranteUseCase.ObtenerEstadoMenu(r),
                r.GustosQueSirve.Select(g => new GustoSugeridoMenu(g.Id, g.Nombre)).ToArray()))
            .ToArray();
    }
}
