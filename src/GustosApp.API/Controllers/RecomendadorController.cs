using GustosApp.API.DTO;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GustosApp.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
   
    public class RecomendadorController : ControllerBase
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IRestauranteRepository _restauranteRepository;
        private readonly RecomendacionIAUseCase _recomendacionIAUseCase;

        public RecomendadorController(
            IUsuarioRepository usuarioRepository,
            IRestauranteRepository restauranteRepository,
            RecomendacionIAUseCase recomendacionIAUseCase)
        {
            _usuarioRepository = usuarioRepository;
            _restauranteRepository = restauranteRepository;
            _recomendacionIAUseCase = recomendacionIAUseCase;
        }
      
        [HttpGet("{restauranteId}/recomendacion")]
        [EnableRateLimiting("ExplicacionRestaurante")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [ProducesResponseType(typeof(RecomendacionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> ObtenerRecomendacion(Guid restauranteId, CancellationToken ct)
        {
            // 1) Obtener UID del usuario autenticado
            var usuarioId = User.FindFirst("user_id")?.Value;

            if (usuarioId == null)
                return BadRequest("No se pudo obtener el usuario autenticado.");

            // 2) Obtener datos del usuario y del restaurante desde 
            var usuario = await _usuarioRepository.GetByFirebaseUidAsync(usuarioId, ct);

            var restaurante = await _restauranteRepository.GetByIdAsync(restauranteId,ct);

            if (usuario == null || restaurante == null)
                return NotFound();

            // 3) Armar prompt
            var explicacion = await _recomendacionIAUseCase.Handle(usuario, restaurante,ct);

            // También descartar explicaciones cacheadas o generadas mientras se desactivaba la ficha.
            var estadoActual = await _restauranteRepository.GetRestauranteByIdAsync(restauranteId, ct);
            if (estadoActual == null || estadoActual.Desactivado)
                return Conflict(new { message = "Este restaurante no está disponible para recomendaciones." });

            return Ok(new RecomendacionResponse
            {
                RestauranteId = restauranteId,
                Explicacion = explicacion
            });
        }


    }
}
