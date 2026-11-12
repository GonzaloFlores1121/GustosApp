using System.Security.Claims;
using GustosApp.API.DTO;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model.@enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GustosApp.API.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public sealed class PagoController : ControllerBase
{
    private readonly IPagoService _pagos;
    private readonly IUsuarioRepository _usuarios;
    private readonly ILogger<PagoController> _logger;

    public PagoController(IPagoService pagos, IUsuarioRepository usuarios, ILogger<PagoController> logger)
    {
        _pagos = pagos;
        _usuarios = usuarios;
        _logger = logger;
    }

    [HttpGet("beneficios")]
    [AllowAnonymous]
    public IActionResult ObtenerBeneficiosPremium() => Ok(_pagos.ObtenerCatalogo());

    [HttpPost("crear")]
    public async Task<IActionResult> CrearPago(CancellationToken ct)
    {
        try
        {
            var compra = await _pagos.IniciarCompraAsync(GetFirebaseUid(), ct);
            return Ok(new CrearPagoResponse
            {
                CompraId = compra.CompraId,
                InitPoint = compra.UrlCheckout,
                Status = compra.Estado,
                Reutilizada = compra.Reutilizada
            });
        }
        catch (InvalidOperationException)
        {
            return Conflict(new ProblemDetails { Title = "No se pudo iniciar el pago", Detail = "El pago no está disponible para esta cuenta en este momento.", Status = 409 });
        }
        catch (PagoTemporalmenteNoDisponibleException)
        {
            return StatusCode(503, new ProblemDetails { Title = "No pudimos confirmar el pago", Detail = "La operación quedó guardada para volver a intentarlo sin duplicarla.", Status = 503 });
        }
    }

    [HttpGet("compras/{compraId:guid}")]
    public async Task<IActionResult> ObtenerCompra(Guid compraId, [FromQuery] bool reconciliar = true, CancellationToken ct = default)
    {
        try { return Ok(await _pagos.ObtenerEstadoCompraAsync(GetFirebaseUid(), compraId, reconciliar, ct)); }
        catch (KeyNotFoundException)
        {
            // La misma respuesta evita revelar si el identificador pertenece a otra cuenta.
            return NotFound(new ProblemDetails { Title = "Compra no encontrada", Status = 404 });
        }
        catch (Exception ex) when (ex is HttpRequestException or PagoTemporalmenteNoDisponibleException)
        {
            _logger.LogWarning("No se pudo reconciliar la compra {CompraId}. Tipo {Tipo}", compraId, ex.GetType().Name);
            return StatusCode(503, new ProblemDetails { Title = "Verificación temporalmente no disponible", Status = 503 });
        }
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> WebhookPago([FromBody] WebhookPagoRequest request, CancellationToken ct)
    {
        var ids = Request.Query["data.id"];
        var dataId = ids.Count == 1 ? ids.ToString() : string.Empty;
        var requestId = Request.Headers["x-request-id"].ToString();
        var signature = Request.Headers["x-signature"].ToString();
        if (!_pagos.ValidarFirmaWebhook(dataId, requestId, signature)) return Unauthorized();
        if (request.Type is not ("order" or "orders_v2")) return Ok(new { estado = "ignorado" });
        if (!string.Equals(dataId, request.Data?.Id, StringComparison.OrdinalIgnoreCase)) return BadRequest();

        var resultado = await _pagos.ProcesarNotificacionOrdenAsync(dataId, ct);
        return resultado switch
        {
            ResultadoWebhookPago.FalloTransitorio => StatusCode(503, new { estado = "reintentar" }),
            ResultadoWebhookPago.Procesado => Ok(new { estado = "procesado" }),
            _ => Ok(new { estado = "ignorado" })
        };
    }

    [HttpGet("verificar-estado-premium")]
    public async Task<IActionResult> VerificarEstadoPremium(CancellationToken ct)
    {
        var uid = GetFirebaseUid();
        try { await _pagos.VerificarYProcesarPagosPendientesAsync(uid, ct); }
        catch (Exception ex) when (ex is HttpRequestException or PagoTemporalmenteNoDisponibleException)
        { _logger.LogWarning("Reconciliación Premium no disponible. Tipo {Tipo}", ex.GetType().Name); }
        var usuario = await _usuarios.GetByFirebaseUidAsync(uid, ct);
        if (usuario == null) return NotFound();
        return Ok(new { success = true, isPremium = usuario.Plan == PlanUsuario.Plus, plan = usuario.Plan.ToString() });
    }

    [HttpGet("admin/cobros-duplicados")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> ObtenerCobrosDuplicados(CancellationToken ct) =>
        Ok(await _pagos.ObtenerCobrosDuplicadosAsync(ct));

    private string GetFirebaseUid() =>
        User.FindFirst("user_id")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("No se pudo identificar al usuario.");
}
