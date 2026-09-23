using GustosApp.Application.Interfaces;
using GustosApp.Domain.Model.@enum;
using GustosApp.Domain.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases;
using GustosApp.Application.UseCases.AmistadUseCases;
using AutoMapper;
using GustosApp.API.DTO;
using GustosApp.Application.UseCases.RestauranteUseCases.Importacion;

namespace GustosApp.API.Controllers
{
    [Authorize(Policy = "Admin")]
    [Route("[controller]")]
    [ApiController]

    public class AdminController : BaseApiController
    {
        private readonly AprobarSolicitudRestauranteUseCase _aprobarSolicitud;
        private readonly ObtenerSolicitudRestaurantesPorIdUseCase _getDetalle;
        private readonly RechazarSolicitudRestauranteUseCase _rechazarSolicitud;
        private readonly ObtenerSolicitudesPorTipoUseCase _getPorTipo;
        private readonly IMapper _mapper;
        private readonly ImportarRestaurantesUseCase _importarRestaurantes;
        private readonly DescubrirRestaurantesCercanosUseCase _descubrirRestaurantes;
       
        public AdminController(AprobarSolicitudRestauranteUseCase aprobarSolicitud,
           ObtenerSolicitudRestaurantesPorIdUseCase getDetalle,
           RechazarSolicitudRestauranteUseCase rechazarSolicitud,
           ObtenerSolicitudesPorTipoUseCase getPorTipo,
           ImportarRestaurantesUseCase importarRestaurantes,
           DescubrirRestaurantesCercanosUseCase descubrirRestaurantes,
            IMapper mapper)
        {
            _aprobarSolicitud = aprobarSolicitud;
            _getDetalle = getDetalle;
            _rechazarSolicitud = rechazarSolicitud;
            _getPorTipo = getPorTipo;
            _importarRestaurantes = importarRestaurantes;
            _descubrirRestaurantes = descubrirRestaurantes;
            _mapper = mapper;
        }

 
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(SolicitudRestauranteDetalleDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetDetalle(Guid id, CancellationToken ct)
        {
            var result = await _getDetalle.HandleAsync(id, ct);
            var response = _mapper.Map<SolicitudRestauranteDetalleDto>(result);

            return result is null ? NotFound() : Ok(response);
        }

        [HttpPost("solicitudes/aprobar/{id:guid}")]
        [ProducesResponseType(typeof(Restaurante), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]

        public async Task<IActionResult> AprobarSolicitud(Guid id, CancellationToken ct)
        {
            var restaurante = await _aprobarSolicitud.HandleAsync(id, ct);

            return Ok(restaurante);
        }

        [HttpPost("solicitudes/{id:guid}/reprocesar-menu")]
        public async Task<IActionResult> ReprocesarMenu(Guid id, CancellationToken ct)
        {
            await _aprobarSolicitud.ReprocesarMenuAsync(id, ct);
            return NoContent();
        }

        [HttpPost("restaurantes/importar")]
        [ProducesResponseType(typeof(ResultadoImportacionRestaurantes), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ImportarRestaurantes(
            [FromBody] ImportacionRestaurantesDto solicitud,
            CancellationToken ct)
        {
            try
            {
                var resultado = await _importarRestaurantes.HandleAsync(
                    solicitud.Restaurantes,
                    solicitud.Confirmar,
                    ct);

                return Ok(resultado);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("restaurantes/descubrir-cercanos")]
        [ProducesResponseType(
      typeof(ResultadoDescubrimientoRestaurantes),
      StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> DescubrirRestaurantesCercanos(
      [FromBody] DescubrimientoRestaurantesDto solicitud,
      CancellationToken ct)
        {
            try
            {
                var resultado = await _descubrirRestaurantes.HandleAsync(
                    new SolicitudDescubrimientoRestaurantes(
                        solicitud.Latitud,
                        solicitud.Longitud,
                        solicitud.RadioMetros,
                        solicitud.CantidadMaxima),
                    ct);

                return Ok(resultado);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(
                    StatusCodes.Status502BadGateway,
                    new
                    {
                        error = "Google Places no pudo completar la búsqueda.",
                        googleStatusCode = (int?)ex.StatusCode,
                        detalle = ex.Message
                    });
            }
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RechazarSolicitud(Guid id,
           [FromBody] string motivoRechazo,
           CancellationToken ct)
        {
            await _rechazarSolicitud.HandleAsync(id, motivoRechazo, ct);
            return Ok(new { message = "Solicitud rechazada correctamente." });
        }

        [HttpGet("solicitudes")]
        [ProducesResponseType(typeof(SolicitudRestaurantePendienteDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetPorTipo(
         [FromQuery] EstadoSolicitudRestaurante tipo = EstadoSolicitudRestaurante.Pendiente,
         CancellationToken ct = default)
        {
            var result = await _getPorTipo.HandleAsync(tipo, ct);

            var response = _mapper.Map<List<SolicitudRestaurantePendienteDto>>(result);

            return Ok(response);
        }


    }
}
