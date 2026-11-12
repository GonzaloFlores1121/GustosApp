using GustosApp.API.Extensiones;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace GustosApp.API.Middleware;

/// <summary>
/// Autoriza roles sensibles contra Firebase y la base de datos en cada solicitud.
/// No usa caché: retirar un rol tiene efecto en la siguiente petición, a cambio de
/// una consulta a Firebase y otra a la base de datos por operación protegida.
/// </summary>
public sealed class RolVigenteHandler(
    IFirebaseAuthService firebase,
    IUsuarioRepository usuarios,
    ILogger<RolVigenteHandler> logger)
    : AuthorizationHandler<RolVigenteRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RolVigenteRequirement requirement)
    {
        if (context.Resource is not HttpContext httpContext)
            return;

        var uid = context.User.FindFirst("user_id")?.Value
                  ?? context.User.FindFirst("sub")?.Value;
        var token = JwtExtensions.ObtenerTokenAutenticacion(httpContext.Request);

        if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(token))
            return;

        try
        {
            var tokenVigente = await firebase.VerifyIdTokenAsync(
                token,
                comprobarRevocacion: true,
                httpContext.RequestAborted);

            if (!string.Equals(tokenVigente.Uid, uid, StringComparison.Ordinal))
                return;

            var usuario = await usuarios.GetByFirebaseUidAsync(uid, httpContext.RequestAborted);
            if (usuario?.Rol == requirement.Rol)
                context.Succeed(requirement);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo validar el rol vigente para una operación protegida.");
        }
    }
}
