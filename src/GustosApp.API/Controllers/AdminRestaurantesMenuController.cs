using GustosApp.Application.UseCases.RestauranteUseCases.Importacion;
using GustosApp.Infraestructure.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GustosApp.API.Controllers;

[Authorize(Policy = "Admin")]
[ApiController]
[Route("Admin/restaurantes/menu")]
public sealed class AdminRestaurantesMenuController : ControllerBase
{
    [HttpGet("pendientes")]
    public async Task<IActionResult> ObtenerPendientes(
        [FromServices] ObtenerPendientesClasificacionRestauranteUseCase useCase,
        CancellationToken ct) => Ok(await useCase.HandleAsync(ct));

    [HttpGet("buscar")]
    public async Task<IActionResult> Buscar(
        [FromQuery] string texto,
        [FromServices] BuscarRestaurantesParaGestionMenuUseCase useCase,
        CancellationToken ct)
    {
        try { return Ok(await useCase.HandleAsync(texto, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("{restauranteId:guid}")]
    public async Task<IActionResult> ObtenerDetalle(
        Guid restauranteId,
        [FromServices] ObtenerDetalleGestionRestauranteUseCase useCase,
        CancellationToken ct)
    {
        try { return Ok(await useCase.HandleAsync(restauranteId, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPost("{restauranteId:guid}/analizar")]
    [RequestSizeLimit(15_000_000)]
    public async Task<IActionResult> Analizar(
        Guid restauranteId,
        [FromForm] AnalizarMenuRestauranteRequest request,
        [FromServices] AnalizarMenuRestauranteImportadoUseCase useCase,
        CancellationToken ct)
    {
        var archivos = request.Imagenes ?? [];
        if (archivos.Count > 5) return BadRequest(new { error = "Podés adjuntar hasta cinco imágenes." });

        try
        {
            foreach (var archivo in archivos)
            {
                await using var contenido = archivo.OpenReadStream();
                await ValidadorContenidoImagen.ValidarAsync(contenido, archivo.FileName, ct: ct);
            }
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var streams = archivos.Select(a => a.OpenReadStream()).ToArray();
        try { return Ok(await useCase.HandleAsync(restauranteId, request.Texto, streams, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
        finally { foreach (var stream in streams) await stream.DisposeAsync(); }
    }

    [HttpPut("{restauranteId:guid}")]
    public async Task<IActionResult> Confirmar(
        Guid restauranteId,
        [FromBody] ConfirmarMenuRestauranteRequest request,
        [FromServices] ConfirmarMenuRestauranteImportadoUseCase useCase,
        CancellationToken ct)
    {
        try
        {
            await useCase.HandleAsync(restauranteId, request.Texto, request.Categoria, request.GustoIds ?? [], ct);
            return NoContent();
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPut("{restauranteId:guid}/gustos")]
    public async Task<IActionResult> GuardarClasificacion(
        Guid restauranteId,
        [FromBody] GuardarClasificacionRestauranteRequest request,
        [FromServices] GuardarClasificacionRestauranteImportadoUseCase useCase,
        CancellationToken ct)
    {
        try
        {
            await useCase.HandleAsync(restauranteId, request.Categoria, request.GustoIds ?? [], ct);
            return NoContent();
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
}

public sealed class AnalizarMenuRestauranteRequest
{
    public string? Texto { get; set; }
    public List<IFormFile>? Imagenes { get; set; }
}

public sealed class ConfirmarMenuRestauranteRequest
{
    public string Texto { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public List<Guid>? GustoIds { get; set; }
}

public sealed class GuardarClasificacionRestauranteRequest
{
    public string Categoria { get; set; } = string.Empty;
    public List<Guid>? GustoIds { get; set; }
}
