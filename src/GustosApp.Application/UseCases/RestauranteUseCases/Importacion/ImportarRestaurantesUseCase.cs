using System.Text.Json;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;

namespace GustosApp.Application.UseCases.RestauranteUseCases.Importacion;

public sealed class ImportarRestaurantesUseCase
{
    public const int CantidadMaximaPorLote = 500;
    private static readonly HashSet<string> TiposGastronomicos = new(StringComparer.OrdinalIgnoreCase)
    {
        "bakery",
        "bar",
        "bar_and_grill",
        "cafe",
        "coffee_shop",
        "deli",
        "dessert_shop",
        "food",
        "food_court",
        "ice_cream_shop",
        "meal_delivery",
        "meal_takeaway",
        "pub",
        "restaurant",
        "sandwich_shop",
        "snacks"
    };

    private readonly IRestauranteRepository _restauranteRepository;
    private readonly TimeProvider _timeProvider;

    public ImportarRestaurantesUseCase(
        IRestauranteRepository restauranteRepository,
        TimeProvider timeProvider)
    {
        _restauranteRepository = restauranteRepository;
        _timeProvider = timeProvider;
    }

    public async Task<ResultadoImportacionRestaurantes> HandleAsync(
        IReadOnlyCollection<RestauranteImportacionEntrada> entradas,
        bool confirmar,
        CancellationToken ct = default)
    {
        ValidarLote(entradas);

        var placeIds = entradas
            .Select(entrada => NormalizarPlaceId(entrada.PlaceId))
            .Where(placeId => placeId.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var existentes = await _restauranteRepository.ObtenerPorPlaceIdsAsync(placeIds, ct);
        var existentesPorPlaceId = existentes.ToDictionary(
            restaurante => restaurante.PlaceId,
            StringComparer.OrdinalIgnoreCase);
        var procesados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<ItemImportacionRestaurante>(entradas.Count);
        var fechaActualUtc = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entrada in entradas)
        {
            ct.ThrowIfCancellationRequested();
            ValidarEntrada(entrada);

            var placeId = NormalizarPlaceId(entrada.PlaceId);
            if (!procesados.Add(placeId))
            {
                items.Add(new ItemImportacionRestaurante(
                    placeId,
                    entrada.Nombre,
                    AccionImportacionRestaurante.DuplicadoEnLote,
                    "El mismo PlaceId aparece más de una vez en el lote."));
                continue;
            }

            if (!TieneOfertaGastronomica(entrada))
            {
                items.Add(new ItemImportacionRestaurante(
                    placeId,
                    entrada.Nombre,
                    AccionImportacionRestaurante.DescartarSinOfertaGastronomica,
                    "Las categorías informadas no indican que el lugar ofrezca comida o bebidas preparadas."));
                continue;
            }

            if (!existentesPorPlaceId.TryGetValue(placeId, out var restaurante))
            {
                var fechaDatosUtc = NormalizarFechaUtc(entrada.FechaDatosUtc ?? fechaActualUtc);
                items.Add(new ItemImportacionRestaurante(
                    placeId,
                    entrada.Nombre,
                    AccionImportacionRestaurante.Crear));

                if (confirmar)
                {
                    await _restauranteRepository.AddAsync(
                        CrearRestaurante(entrada, placeId, fechaDatosUtc, fechaActualUtc),
                        ct);
                }

                continue;
            }

            var fechaDatosExistenteUtc = NormalizarFechaUtc(
                entrada.FechaDatosUtc ?? restaurante.UltimaActualizacion);

            if (restaurante.DuenoId.HasValue || !string.IsNullOrWhiteSpace(restaurante.PropietarioUid))
            {
                items.Add(new ItemImportacionRestaurante(
                    placeId,
                    entrada.Nombre,
                    AccionImportacionRestaurante.RequiereRevision,
                    "El restaurante tiene propietario y no se sobrescribe automáticamente."));
                continue;
            }

            if (fechaDatosExistenteUtc < NormalizarFechaUtc(restaurante.UltimaActualizacion))
            {
                items.Add(new ItemImportacionRestaurante(
                    placeId,
                    entrada.Nombre,
                    AccionImportacionRestaurante.IgnorarPorAntiguedad,
                    "Los datos recibidos son anteriores a los ya almacenados."));
                continue;
            }

            if (!TieneCambios(restaurante, entrada, placeId, fechaDatosExistenteUtc))
            {
                items.Add(new ItemImportacionRestaurante(
                    placeId,
                    entrada.Nombre,
                    AccionImportacionRestaurante.SinCambios));
                continue;
            }

            items.Add(new ItemImportacionRestaurante(
                placeId,
                entrada.Nombre,
                AccionImportacionRestaurante.Actualizar));

            if (confirmar)
            {
                AplicarDatos(restaurante, entrada, placeId, fechaDatosExistenteUtc);
                await _restauranteRepository.UpdateAsync(restaurante, ct);
            }
        }

        if (confirmar && items.Any(item =>
                item.Accion is AccionImportacionRestaurante.Crear or AccionImportacionRestaurante.Actualizar))
        {
            await _restauranteRepository.SaveChangesAsync(ct);
        }

        return CrearResultado(confirmar, items);
    }

    private static void ValidarLote(IReadOnlyCollection<RestauranteImportacionEntrada> entradas)
    {
        ArgumentNullException.ThrowIfNull(entradas);

        if (entradas.Count == 0)
        {
            throw new ArgumentException("El lote debe contener al menos un restaurante.", nameof(entradas));
        }

        if (entradas.Count > CantidadMaximaPorLote)
        {
            throw new ArgumentException(
                $"El lote no puede superar los {CantidadMaximaPorLote} restaurantes.",
                nameof(entradas));
        }
    }

    private static void ValidarEntrada(RestauranteImportacionEntrada entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        if (string.IsNullOrWhiteSpace(entrada.PlaceId))
            throw new ArgumentException("Todos los restaurantes importados deben tener PlaceId.");
        if (string.IsNullOrWhiteSpace(entrada.Nombre))
            throw new ArgumentException($"El restaurante {entrada.PlaceId} no tiene nombre.");
        if (string.IsNullOrWhiteSpace(entrada.Direccion))
            throw new ArgumentException($"El restaurante {entrada.PlaceId} no tiene dirección.");
        if (entrada.Latitud is < -90 or > 90)
            throw new ArgumentException($"El restaurante {entrada.PlaceId} tiene una latitud inválida.");
        if (entrada.Longitud is < -180 or > 180)
            throw new ArgumentException($"El restaurante {entrada.PlaceId} tiene una longitud inválida.");
        if (entrada.Rating is < 0 or > 5)
            throw new ArgumentException($"El restaurante {entrada.PlaceId} tiene un rating inválido.");
        if (entrada.CantidadResenas < 0)
            throw new ArgumentException($"El restaurante {entrada.PlaceId} tiene una cantidad de reseñas inválida.");
        if (string.IsNullOrWhiteSpace(entrada.PrimaryType))
            throw new ArgumentException($"El restaurante {entrada.PlaceId} no tiene tipo principal.");

        ValidarJson(entrada.HorariosJson, entrada.PlaceId, "horarios");
        ValidarJson(entrada.TypesJson, entrada.PlaceId, "tipos", JsonValueKind.Array);
    }

    private static void ValidarJson(
        string json,
        string placeId,
        string campo,
        JsonValueKind? tipoRaizEsperado = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException(
                $"El restaurante {placeId} no tiene JSON en {campo}.");
        }

        try
        {
            using var documento = JsonDocument.Parse(json);
            if (tipoRaizEsperado.HasValue && documento.RootElement.ValueKind != tipoRaizEsperado.Value)
            {
                throw new ArgumentException(
                    $"El restaurante {placeId} debe informar {campo} como un arreglo JSON.");
            }
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                $"El restaurante {placeId} tiene JSON inválido en {campo}.",
                ex);
        }
    }

    private static bool TieneOfertaGastronomica(RestauranteImportacionEntrada entrada)
    {
        if (EsTipoGastronomico(entrada.PrimaryType))
        {
            return true;
        }

        using var documento = JsonDocument.Parse(entrada.TypesJson);
        return documento.RootElement
            .EnumerateArray()
            .Where(elemento => elemento.ValueKind == JsonValueKind.String)
            .Select(elemento => elemento.GetString())
            .Any(tipo => tipo is not null && EsTipoGastronomico(tipo));
    }

    private static bool EsTipoGastronomico(string tipo)
    {
        var tipoNormalizado = tipo.Trim();
        return TiposGastronomicos.Contains(tipoNormalizado) ||
               tipoNormalizado.EndsWith("_restaurant", StringComparison.OrdinalIgnoreCase);
    }

    private static Restaurante CrearRestaurante(
        RestauranteImportacionEntrada entrada,
        string placeId,
        DateTime fechaDatosUtc,
        DateTime fechaActualUtc)
    {
        var restaurante = new Restaurante
        {
            Id = Guid.NewGuid(),
            CreadoUtc = fechaActualUtc
        };

        AplicarDatos(restaurante, entrada, placeId, fechaDatosUtc);
        return restaurante;
    }

    private static void AplicarDatos(
        Restaurante restaurante,
        RestauranteImportacionEntrada entrada,
        string placeId,
        DateTime fechaDatosUtc)
    {
        restaurante.PlaceId = placeId;
        restaurante.Nombre = entrada.Nombre.Trim();
        restaurante.NombreNormalizado = entrada.Nombre.Trim().ToLowerInvariant();
        restaurante.Direccion = entrada.Direccion.Trim();
        restaurante.Latitud = entrada.Latitud;
        restaurante.Longitud = entrada.Longitud;
        restaurante.HorariosJson = EsJsonVacio(entrada.HorariosJson) && !EsJsonVacio(restaurante.HorariosJson)
            ? restaurante.HorariosJson
            : entrada.HorariosJson;
        restaurante.Rating = entrada.Rating ?? restaurante.Rating;
        restaurante.CantidadResenas = entrada.CantidadResenas ?? restaurante.CantidadResenas;
        restaurante.Categoria = LimpiarOpcional(entrada.Categoria) ?? restaurante.Categoria;
        restaurante.WebUrl = LimpiarOpcional(entrada.WebUrl) ?? restaurante.WebUrl;
        restaurante.PrimaryType = string.IsNullOrWhiteSpace(entrada.PrimaryType)
            ? "restaurant"
            : entrada.PrimaryType.Trim();
        restaurante.TypesJson = EsArregloJsonVacio(entrada.TypesJson) && !EsArregloJsonVacio(restaurante.TypesJson)
            ? restaurante.TypesJson
            : entrada.TypesJson;
        restaurante.ImagenUrl = LimpiarOpcional(entrada.ImagenUrl) ?? restaurante.ImagenUrl;
        restaurante.ActualizadoUtc = fechaDatosUtc;
        restaurante.UltimaActualizacion = fechaDatosUtc;
    }

    private static bool TieneCambios(
        Restaurante restaurante,
        RestauranteImportacionEntrada entrada,
        string placeId,
        DateTime fechaDatosUtc)
    {
        var copia = new Restaurante
        {
            PlaceId = restaurante.PlaceId,
            Nombre = restaurante.Nombre,
            NombreNormalizado = restaurante.NombreNormalizado,
            Direccion = restaurante.Direccion,
            Latitud = restaurante.Latitud,
            Longitud = restaurante.Longitud,
            HorariosJson = restaurante.HorariosJson,
            Rating = restaurante.Rating,
            CantidadResenas = restaurante.CantidadResenas,
            Categoria = restaurante.Categoria,
            WebUrl = restaurante.WebUrl,
            PrimaryType = restaurante.PrimaryType,
            TypesJson = restaurante.TypesJson,
            ImagenUrl = restaurante.ImagenUrl,
            ActualizadoUtc = restaurante.ActualizadoUtc,
            UltimaActualizacion = restaurante.UltimaActualizacion
        };

        AplicarDatos(copia, entrada, placeId, fechaDatosUtc);

        return restaurante.PlaceId != copia.PlaceId ||
               restaurante.Nombre != copia.Nombre ||
               restaurante.NombreNormalizado != copia.NombreNormalizado ||
               restaurante.Direccion != copia.Direccion ||
               restaurante.Latitud != copia.Latitud ||
               restaurante.Longitud != copia.Longitud ||
               restaurante.HorariosJson != copia.HorariosJson ||
               restaurante.Rating != copia.Rating ||
               restaurante.CantidadResenas != copia.CantidadResenas ||
               restaurante.Categoria != copia.Categoria ||
               restaurante.WebUrl != copia.WebUrl ||
               restaurante.PrimaryType != copia.PrimaryType ||
               restaurante.TypesJson != copia.TypesJson ||
               restaurante.ImagenUrl != copia.ImagenUrl ||
               NormalizarFechaUtc(restaurante.UltimaActualizacion) != copia.UltimaActualizacion;
    }

    private static ResultadoImportacionRestaurantes CrearResultado(
        bool confirmar,
        IReadOnlyCollection<ItemImportacionRestaurante> items)
    {
        var creados = items.Count(item => item.Accion == AccionImportacionRestaurante.Crear);
        var actualizados = items.Count(item => item.Accion == AccionImportacionRestaurante.Actualizar);
        var sinCambios = items.Count(item => item.Accion == AccionImportacionRestaurante.SinCambios);

        return new ResultadoImportacionRestaurantes(
            confirmar,
            items.Count,
            creados,
            actualizados,
            sinCambios,
            items.Count - creados - actualizados - sinCambios,
            items);
    }

    private static string NormalizarPlaceId(string placeId) => placeId.Trim();

    private static string? LimpiarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static bool EsJsonVacio(string json) => json.Trim() == "{}";

    private static bool EsArregloJsonVacio(string json) => json.Trim() == "[]";

    private static DateTime NormalizarFechaUtc(DateTime fecha) => fecha.Kind switch
    {
        DateTimeKind.Utc => fecha,
        DateTimeKind.Local => fecha.ToUniversalTime(),
        _ => DateTime.SpecifyKind(fecha, DateTimeKind.Utc)
    };
}
