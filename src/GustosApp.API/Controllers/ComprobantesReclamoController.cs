using GustosApp.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GustosApp.API.Controllers;

[ApiController]
[Authorize]
[Route("api/solicitudes-restaurantes")]
public sealed class ComprobantesReclamoController(ISolicitudRestauranteRepository solicitudes,
    IUsuarioRepository usuarios, IAuthorizationService autorizacion) : BaseApiController
{
    [HttpGet("{id:guid}/comprobante")]
    public async Task<IActionResult> Descargar(Guid id, CancellationToken ct)
    {
        var solicitud = await solicitudes.GetByIdAsync(id, ct);
        if (solicitud is null) return NotFound();
        var esAdmin = (await autorizacion.AuthorizeAsync(User, "Admin")).Succeeded;
        if (!esAdmin)
        {
            var usuario = await usuarios.GetByFirebaseUidAsync(GetFirebaseUid(), ct);
            if (usuario is null || usuario.Id != solicitud.UsuarioId) return Forbid();
        }
        if (solicitud.ComprobanteReclamo is null || solicitud.TipoComprobante is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
        var extension = solicitud.TipoComprobante switch { "application/pdf" => "pdf", "image/png" => "png", _ => "jpg" };
        return File(solicitud.ComprobanteReclamo, solicitud.TipoComprobante, $"comprobante.{extension}");
    }
}
