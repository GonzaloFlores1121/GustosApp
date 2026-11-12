using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GustosApp.API.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class AutenticacionController(
    IFirebaseAuthService firebase,
    IUsuarioRepository usuarios,
    ILogger<AutenticacionController> logger) : BaseApiController
{
    [Authorize]
    [HttpPost("refresh-claims")]
    public async Task<IActionResult> RefreshClaims(CancellationToken ct)
    {
        var uid = GetFirebaseUid();
        if (string.IsNullOrWhiteSpace(uid)) return Unauthorized(new { code = "INVALID_IDENTITY" });

        var usuario = await usuarios.GetByFirebaseUidAsync(uid, ct);
        if (usuario == null)
            return Conflict(new { code = "REGISTRATION_PENDING", message = "Terminá el registro para continuar." });

        try
        {
            await firebase.SetUserRoleAsync(uid, usuario.Rol.ToString());
            return Ok(new { message = "Claims actualizados", rol = usuario.Rol.ToString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudieron actualizar los permisos del usuario {FirebaseUid}", uid);
            return StatusCode(503, new { code = "CLAIMS_UNAVAILABLE", message = "No pudimos actualizar tus permisos. Reintentá." });
        }
    }
}
