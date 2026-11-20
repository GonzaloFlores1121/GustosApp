using GustosApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using GustosApp.Domain.Interfaces;

namespace GustosApp.API.Controllers;

[ApiController]
[Authorize]
[Route("api/restaurantes/{id:guid}/foto-google")]
[EnableRateLimiting("FotosGoogle")]
public sealed class FotosGoogleController(IFotoGoogleService fotos, IUsuarioRepository usuarios) : BaseApiController
{
    [HttpGet("{referencia}")]
    public async Task<IActionResult> Obtener(Guid id, string referencia, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (await usuarios.GetByFirebaseUidAsync(GetFirebaseUid(), ct) == null) return Forbid();
        var resultado = await fotos.ObtenerAsync(id, referencia, ct);
        return resultado == null ? NotFound(new { message = "No pudimos cargar esta foto." }) : Ok(resultado);
    }
}
