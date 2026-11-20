using System.Text.Json;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Common;
using GustosApp.Domain.Model;
using Microsoft.Extensions.Logging;

namespace GustosApp.Application.UseCases.RestauranteUseCases;

public sealed class GestionarBorradorMenuRestauranteUseCase
{
    private readonly IRestauranteRepository _restaurantes;
    private readonly IRestauranteMenuRepository _menus;
    private readonly IFileStorageService _archivos;
    private readonly IHttpDownloader _downloader;
    private readonly IOcrService _ocr;
    private readonly IMenuParser _parser;
    private readonly ILogger<GestionarBorradorMenuRestauranteUseCase>? _logger;

    public GestionarBorradorMenuRestauranteUseCase(
        IRestauranteRepository restaurantes,
        IRestauranteMenuRepository menus,
        IFileStorageService archivos,
        IHttpDownloader downloader,
        IOcrService ocr,
        IMenuParser parser,
        ILogger<GestionarBorradorMenuRestauranteUseCase>? logger = null)
    {
        _restaurantes = restaurantes;
        _menus = menus;
        _archivos = archivos;
        _downloader = downloader;
        _ocr = ocr;
        _parser = parser;
        _logger = logger;
    }

    public async Task<RestauranteMenu> CrearDesdeArchivoAsync(
        Guid restauranteId,
        Guid usuarioId,
        ArchivoEntrada archivo,
        CancellationToken ct)
    {
        var restaurante = await ObtenerYValidarPropietarioAsync(restauranteId, usuarioId, ct);
        string? urlNueva = null;
        try
        {
            using var stream = archivo.Stream;
            urlNueva = await _archivos.UploadFileAsync(stream, archivo.FileName, $"restaurantes/menus-borrador/{restauranteId:N}");
            return await CrearYProcesarAsync(restaurante, urlNueva, eliminarFuenteReemplazada: true, ct);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(urlNueva))
            {
                // El reintento diferido vuelve a verificar todas las referencias. Si el
                // borrador llegó a persistirse, conservará la imagen.
                try { await _archivos.ScheduleCleanupAsync(urlNueva, $"restaurantes/menus-borrador/{restauranteId:N}"); }
                catch (Exception ex) { _logger?.LogWarning(ex, "No se pudo programar limpieza de menú {RestauranteId}.", restauranteId); }
            }
            throw;
        }
    }

    public async Task<RestauranteMenu?> ObtenerAsync(
        Guid restauranteId,
        Guid usuarioId,
        CancellationToken ct)
    {
        await ObtenerYValidarPropietarioAsync(restauranteId, usuarioId, ct);
        return await _menus.GetByRestauranteIdAsync(restauranteId, ct);
    }

    public async Task<RestauranteMenu> CrearDesdeUrlDeSolicitudAsync(
        Restaurante restaurante,
        string url,
        CancellationToken ct)
    {
        var menu = await _menus.GetByRestauranteIdAsync(restaurante.Id, ct);
        if (menu != null &&
            (menu.BorradorToken.HasValue || restaurante.MenuProcesado == true || menu.Version > 0))
            throw new InvalidOperationException("El restaurante ya tiene un menú o borrador más reciente.");

        return await CrearYProcesarAsync(restaurante, url, eliminarFuenteReemplazada: false, ct);
    }

    public async Task<string?> PublicarAsync(
        Guid restauranteId,
        Guid usuarioId,
        Guid token,
        string jsonRevisado,
        CancellationToken ct)
    {
        var restaurante = await ObtenerYValidarPropietarioAsync(restauranteId, usuarioId, ct);
        ValidarMenuJson(jsonRevisado);
        var menu = await _menus.GetByRestauranteIdAsync(restauranteId, ct)
            ?? throw new InvalidOperationException("No hay un borrador para publicar.");

        var imagenAnterior = menu.PublicarBorrador(token, jsonRevisado, DateTime.UtcNow);
        restaurante.MenuProcesado = true;
        restaurante.MenuError = null;
        restaurante.RegistrarCambioMenuPublicado();
        restaurante.ActualizadoUtc = DateTime.UtcNow;
        await _menus.UpdateAsync(menu, ct);
        await _restaurantes.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(imagenAnterior) &&
            !string.Equals(imagenAnterior, menu.ImagenFuentePublicadaUrl, StringComparison.Ordinal))
        {
            await _archivos.DeleteFileAsync(imagenAnterior, $"restaurantes/menus-borrador/{restauranteId:N}");
        }

        return menu.ImagenFuentePublicadaUrl;
    }

    public async Task DescartarAsync(
        Guid restauranteId,
        Guid usuarioId,
        Guid token,
        CancellationToken ct)
    {
        await ObtenerYValidarPropietarioAsync(restauranteId, usuarioId, ct);
        var menu = await _menus.GetByRestauranteIdAsync(restauranteId, ct)
            ?? throw new InvalidOperationException("No hay un borrador para descartar.");
        var url = menu.DescartarBorrador(token);
        await _menus.UpdateAsync(menu, ct);

        if (!string.IsNullOrWhiteSpace(url) &&
            !string.Equals(url, menu.ImagenFuentePublicadaUrl, StringComparison.Ordinal))
        {
            await _archivos.DeleteFileAsync(url, $"restaurantes/menus-borrador/{restauranteId:N}");
        }
    }

    private async Task<RestauranteMenu> CrearYProcesarAsync(
        Restaurante restaurante,
        string url,
        bool eliminarFuenteReemplazada,
        CancellationToken ct)
    {
        var menu = await _menus.GetByRestauranteIdAsync(restaurante.Id, ct);
        if (menu == null)
        {
            menu = new RestauranteMenu
            {
                RestauranteId = restaurante.Id,
                Json = "{}",
                Version = 0,
                FechaActualizacionUtc = DateTime.UtcNow
            };
            await _menus.AddAsync(menu, ct);
        }

        var fuenteBorradorAnterior = menu.BorradorImagenFuenteUrl;
        var token = Guid.NewGuid();
        menu.IniciarBorrador(token, url, DateTime.UtcNow, OrigenImagenRestaurante.Propietario);
        await _menus.UpdateAsync(menu, ct);

        if (eliminarFuenteReemplazada && !string.IsNullOrWhiteSpace(fuenteBorradorAnterior) &&
            !string.Equals(fuenteBorradorAnterior, menu.ImagenFuentePublicadaUrl, StringComparison.Ordinal) &&
            !string.Equals(fuenteBorradorAnterior, url, StringComparison.Ordinal))
        {
            await _archivos.DeleteFileAsync(fuenteBorradorAnterior, $"restaurantes/menus-borrador/{restaurante.Id:N}");
        }

        try
        {
            var bytes = await _downloader.DownloadAsync(url, ct);
            using var stream = new MemoryStream(bytes);
            var texto = await _ocr.ReconocerTextoAsync([stream], "spa+eng", ct);
            if (string.IsNullOrWhiteSpace(texto))
                throw new InvalidOperationException("No pudimos detectar texto en la imagen.");

            var json = await _parser.ParsearAsync(texto, "ARS", ct);
            ValidarMenuJson(json);

            await _menus.ReloadAsync(menu, ct);
            if (menu.CompletarBorrador(token, json, DateTime.UtcNow))
                await _menus.UpdateAsync(menu, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Falló el procesamiento del borrador de menú de {RestauranteId}.", restaurante.Id);
            await _menus.ReloadAsync(menu, ct);
            if (menu.MarcarErrorBorrador(
                token,
                "No pudimos procesar la imagen del menú. Podés reemplazarla e intentar nuevamente.",
                DateTime.UtcNow))
                await _menus.UpdateAsync(menu, ct);
        }

        return menu;
    }

    private async Task<Restaurante> ObtenerYValidarPropietarioAsync(
        Guid restauranteId,
        Guid usuarioId,
        CancellationToken ct)
    {
        var restaurante = await _restaurantes.GetRestauranteConImagenesAsync(restauranteId, ct)
            ?? throw new NotFoundException("Restaurante no encontrado.");
        if (restaurante.DuenoId != usuarioId)
            throw new AccesoProhibidoException("No tenés permisos para administrar este menú.");
        return restaurante;
    }

    private static void ValidarMenuJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("El menú revisado no puede estar vacío.");
        try
        {
            using var documento = JsonDocument.Parse(json);
            if (documento.RootElement.ValueKind != JsonValueKind.Object ||
                !documento.RootElement.TryGetProperty("categorias", out var categorias) ||
                categorias.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("El menú revisado no tiene un formato válido.");
        }
        catch (JsonException)
        {
            throw new ArgumentException("El menú revisado no tiene un formato JSON válido.");
        }
    }
}
