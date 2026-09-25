using GustosApp.API.DTO;
using GustosApp.Application.UseCases.NotificacionUseCases;
using GustosApp.Domain.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GustosApp.API.Controllers
{
 
    [ApiController]
    [Route("api/[controller]")]
    public class NotificacionController : BaseApiController
    {
        private readonly EnviarRecomendacionesUsuariosActivosUseCase _enviarRecomendacion;

        public NotificacionController(EnviarRecomendacionesUsuariosActivosUseCase enviarRecomendacion)
        {
            _enviarRecomendacion = enviarRecomendacion;
        }

        
        [Authorize(Policy ="Admin")]
        [HttpPost("recomendar")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Recomendar()
        {
            var uid = GetFirebaseUid();
            var funciono=await _enviarRecomendacion.HandleAsync(uid,CancellationToken.None);
            return Ok(funciono);
        }

        /*
        [HttpGet("{usuarioId:guid}")]
        public async Task<IActionResult> ObtenerNotificaciones(Guid usuarioId, CancellationToken ct)
        {
            var notificaciones = await _ObtenerNotificaciones.HandleAsync(usuarioId, ct);

            if (notificaciones == null || !notificaciones.Any())
                return NotFound("No hay notificaciones para este usuario.");
            
            return Ok(notificaciones);
        }

        [HttpPatch("{usuarioId:guid}/marcar-todas-leidas")]
        public async Task<IActionResult> MarcarTodasLeidas(Guid usuarioId,CancellationToken ct)
        {
            await _marcarNotificion.HandleAsync(usuarioId, ct);
            return NoContent();
        }
    }*/
    }
}
