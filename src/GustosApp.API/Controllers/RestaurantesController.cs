using AutoMapper;
using GustosApp.API.DTO;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Application.UseCases.UsuarioUseCases;
using GustosApp.Domain.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GustosApp.Domain.Model.@enum;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases;
using System.Globalization;
using GustosApp.Application.Common.Exceptions;


// Controlador para restaurantes que se registran en la app por un usuario y restaurantes traidos de Places v1

namespace GustosApp.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class RestaurantesController : BaseApiController
    {
        private readonly ObtenerUsuarioUseCase _obtenerUsuario;
        private readonly CrearSolicitudRestauranteUseCase _solicitudesRestaurantes;
        private readonly BuscarRestaurantesUseCase _buscarRestaurante;
        private readonly ObtenerDatosRegistroRestauranteUseCase _getDatosRegistroRestaurante;
        private readonly IMapper _mapper;
        private readonly AgregarUsuarioRestauranteFavoritoUseCase _agregarFavoritoUseCase;
        private readonly ObtenerMetricasRestauranteUseCase _obtenerMetricasRestauranteUseCase;
        private readonly ActualizarRestauranteDashboardUseCase _actualizarRestauranteDashboardUseCase;
        private readonly ObtenerRestauranteDetalleUseCase _obtenerRestauranteDetalle;
        private readonly IBuscarRestaurantesRecomendadosOrquestador _buscarRestauranteRecomendado;
        private readonly IActualizarImagenesRestauranteUseCase _actualizarImagenesUseCase;
        private readonly EliminarRestauranteUseCase _eliminarRestaurante;
        private readonly ObtenerRestauranteIdPorPropietarioUseCase _obtenerRestauranteIdPorPropietario;

        public RestaurantesController(
      ObtenerUsuarioUseCase obtenerUsuario,
      CrearSolicitudRestauranteUseCase solicitudesRestaurantes,
      ObtenerDatosRegistroRestauranteUseCase getDatosRegistroRestaurante,
      IMapper mapper, BuscarRestaurantesUseCase buscarRestaurante,
      AgregarUsuarioRestauranteFavoritoUseCase agregarUsuarioRestauranteFavoritoUseCase,
      ObtenerMetricasRestauranteUseCase obtenerMetricasRestauranteUseCase,
    ActualizarRestauranteDashboardUseCase actualizarRestauranteDashboardUseCase,
    ObtenerRestauranteDetalleUseCase obtenerRestauranteDetalle,
    IBuscarRestaurantesRecomendadosOrquestador buscarRestauranteRecomendado,
    IActualizarImagenesRestauranteUseCase actualizarImagenesUseCase,
    EliminarRestauranteUseCase eliminarRestaurante,
    ObtenerRestauranteIdPorPropietarioUseCase obtenerRestauranteIdPorPropietario)
        {
            _obtenerUsuario = obtenerUsuario;
            _solicitudesRestaurantes = solicitudesRestaurantes;
            _getDatosRegistroRestaurante = getDatosRegistroRestaurante;
            _mapper = mapper;
            _buscarRestaurante = buscarRestaurante;
            _agregarFavoritoUseCase = agregarUsuarioRestauranteFavoritoUseCase;
            _obtenerMetricasRestauranteUseCase = obtenerMetricasRestauranteUseCase;
            _actualizarRestauranteDashboardUseCase = actualizarRestauranteDashboardUseCase;
            _obtenerRestauranteDetalle = obtenerRestauranteDetalle;
            _buscarRestauranteRecomendado = buscarRestauranteRecomendado;
            _actualizarImagenesUseCase = actualizarImagenesUseCase;
            _eliminarRestaurante = eliminarRestaurante;
            _obtenerRestauranteIdPorPropietario = obtenerRestauranteIdPorPropietario;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(
            [FromQuery] List<string>? gustos,
            [FromQuery] string? amigoUsername,
            CancellationToken ct,
            [FromQuery] double rating,
            [FromQuery(Name = "near.lat")] double? lat,
            [FromQuery(Name = "near.lng")] double? lng,
            [FromQuery(Name = "radiusMeters")] int? radius = 3000,
            [FromQuery] int top = 10
  )
        {
            var firebaseUid = GetFirebaseUid();

            var recommendations = await _buscarRestauranteRecomendado.HandleAsync(
                firebaseUid,gustos,amigoUsername,lat, lng,radius,top,rating,ct);

            // DTO
            var response = _mapper.Map<List<RestauranteDTO>>(recommendations);

            return Ok(new
            {
                total = response.Count,
                recomendaciones = response
            });

        }



        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(RestauranteDetalleDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var uid = GetFirebaseUid();

            var result = await _obtenerRestauranteDetalle.HandleAsync(id, uid, ct);

            var dto = _mapper.Map<RestauranteDetalleDto>(result.Restaurante);
            dto.esFavorito = result.EsFavorito;

            return Ok(dto);
        }




        [HttpPost]
        [RequestSizeLimit(50 * 1024 * 1024)]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CrearSolicitud(
         [FromForm] CrearRestauranteDto dto,
             CancellationToken ct)
        {
            var uid = GetFirebaseUid();

            try
            {
                ArchivoEntrada? imagenDestacada = dto.ImagenDestacada != null ? new ArchivoEntrada(dto.ImagenDestacada.OpenReadStream(), dto.ImagenDestacada.FileName) : null;
                List<ArchivoEntrada>? imagenesInterior = dto.ImagenesInterior?.Select(f => new ArchivoEntrada(f.OpenReadStream(), f.FileName)).ToList();
                List<ArchivoEntrada>? imagenesComidas = dto.ImagenesComidas?.Select(f => new ArchivoEntrada(f.OpenReadStream(), f.FileName)).ToList();
                ArchivoEntrada? imagenMenu = dto.ImagenMenu != null ? new ArchivoEntrada(dto.ImagenMenu.OpenReadStream(), dto.ImagenMenu.FileName) : null;
                ArchivoEntrada? logo = dto.Logo != null ? new ArchivoEntrada(dto.Logo.OpenReadStream(), dto.Logo.FileName) : null;

                var response = await _solicitudesRestaurantes.HandleAsync(
                    uid, dto.Nombre, dto.Direccion, dto.Lat, dto.Lng, dto.HorariosJson,
                    dto.GustosQueSirveIds, dto.RestriccionesQueRespetaIds,
                    imagenDestacada, imagenesInterior, imagenesComidas, imagenMenu, logo,
                    dto.WebsiteUrl, ct);

                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }


        [HttpPost("{id:guid}/reclamo")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ReclamarRestaurante(Guid id,
            [FromServices] ReclamarRestauranteUseCase casoDeUso, CancellationToken ct)
        {
            return Ok(await casoDeUso.HandleAsync(GetFirebaseUid(), id, ct));
        }

        [HttpGet("registro-datos")]
        [ProducesResponseType(typeof(DatosSolicitudRestauranteDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ObtenerDatosParaRegistro(CancellationToken ct)
        {
            var (gustos, restricciones) = await _getDatosRegistroRestaurante.HandleAsync(ct);

            var dto = new DatosSolicitudRestauranteDto
            {
                Gustos = gustos.Select(g => new ItemSimpleDto
                {
                    Id = g.Id,
                    Nombre = g.Nombre
                }).ToList(),
                Restricciones = restricciones.Select(r => new ItemSimpleDto
                {
                    Id = r.Id,
                    Nombre = r.Nombre
                }).ToList()
            };

            return Ok(dto);
        }

        [Authorize(Policy = "DuenoRestaurante")]
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(RestauranteDetalleDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ActualizarBasico(
            Guid id,
            [FromBody] ActualizarRestauranteDashboardRequest dto,
            CancellationToken ct)
        {
            var usuarioId = await ObtenerUsuarioIdActualAsync(ct);
            var restauranteActualizado = await _actualizarRestauranteDashboardUseCase.HandleAsync(
                id,
                usuarioId,
                dto.Direccion,
                dto.Latitud,
                dto.Longitud,
                dto.HorariosJson,
                dto.WebUrl,
                dto.GustosQueSirveIds,
                dto.RestriccionesQueRespetaIds,
                ct);

            var detalle = _mapper.Map<RestauranteDetalleDto>(restauranteActualizado);
            return Ok(detalle);
        }




        [Authorize(Policy = "DuenoRestaurante")]
        [HttpGet("mio")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ObtenerRestauranteIdDueñoRestaurante()
        {
            var firebaseuid = GetFirebaseUid();
            var usuario = await _obtenerUsuario.HandleAsync(FirebaseUid: firebaseuid, ct: CancellationToken.None);
            var restauranteId = await _obtenerRestauranteIdPorPropietario.HandleAsync(usuario.Id);

            return Ok(restauranteId);
        }

        [Authorize(Policy = "DuenoRestaurante")]
        [HttpPut("{id:guid}/imagenes/destacada")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ActualizarImagenDestacada(
            Guid id,
            [FromForm] ActualizarImagenRestauranteRequest request,
            CancellationToken ct = default)
        {
            var usuarioId = await ObtenerUsuarioIdActualAsync(ct);
            ArchivoEntrada? archivoEntrada = null;
            if (request.Archivo != null)
            {
                archivoEntrada = new ArchivoEntrada(
                    request.Archivo.OpenReadStream(), 
                    request.Archivo.FileName
                );
            }

            var url = await _actualizarImagenesUseCase.ActualizarImagenDestacadaAsync(
                id,
                usuarioId,
                archivoEntrada, 
                request.SoloBorrar, 
                ct);

            return Ok(new { imagenDestacada = url });
        }


        [Authorize(Policy = "DuenoRestaurante")]
        [HttpPut("{id:guid}/imagenes/logo")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ActualizarLogo(
          Guid id,
         [FromForm] ActualizarImagenRestauranteRequest request,
         CancellationToken ct = default)
        {
            var usuarioId = await ObtenerUsuarioIdActualAsync(ct);
            ArchivoEntrada? archivoEntrada = null;
            if (request.Archivo != null)
            {
                archivoEntrada = new ArchivoEntrada(
                    request.Archivo.OpenReadStream(), 
                    request.Archivo.FileName
                );
            }

            var url = await _actualizarImagenesUseCase.ActualizarLogoAsync(
                id,
                usuarioId,
                archivoEntrada, 
                request.SoloBorrar, 
                ct);

            return Ok(new { logoUrl = url });
        }


        [Authorize(Policy = "DuenoRestaurante")]
        [HttpPut("{id:guid}/imagenes/interior")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ActualizarImagenesInterior(
            Guid id,
            [FromForm] ActualizarImagenesRestauranteRequest request,
            CancellationToken ct = default)
        {
            var usuarioId = await ObtenerUsuarioIdActualAsync(ct);
            var archivos = request.Archivos?.Select(a => new ArchivoEntrada(a.OpenReadStream(), a.FileName)).ToList();
            var urls = await _actualizarImagenesUseCase.ActualizarImagenesColeccionAsync(
                id, usuarioId, TipoImagenRestaurante.Interior, archivos, request.SoloBorrar, ct);

            return Ok(new { imagenesInterior = urls });
        }


        [Authorize(Policy = "DuenoRestaurante")]
        [HttpPut("{id:guid}/imagenes/comidas")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ActualizarImagenesComida(
            Guid id,
            [FromForm] ActualizarImagenesRestauranteRequest request,
            CancellationToken ct = default)
        {
            var usuarioId = await ObtenerUsuarioIdActualAsync(ct);
            var archivos = request.Archivos?.Select(a => new ArchivoEntrada(a.OpenReadStream(), a.FileName)).ToList();
            var urls = await _actualizarImagenesUseCase.ActualizarImagenesColeccionAsync(
                id, usuarioId, TipoImagenRestaurante.Comida, archivos, request.SoloBorrar, ct);

            return Ok(new { imagenesComida = urls });
        }


        [Authorize(Policy = "DuenoRestaurante")]
        [HttpPut("{id:guid}/imagenes/menu")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ActualizarImagenMenu(
            Guid id,
            [FromForm] ActualizarImagenRestauranteRequest request,
            CancellationToken ct = default)
        {
            var usuarioId = await ObtenerUsuarioIdActualAsync(ct);
            var archivos = request.Archivo != null 
                ? new List<ArchivoEntrada> { new ArchivoEntrada(request.Archivo.OpenReadStream(), request.Archivo.FileName) }
                : null;

            var urls = await _actualizarImagenesUseCase.ActualizarImagenesColeccionAsync(
                id, usuarioId, TipoImagenRestaurante.Menu, archivos, request.SoloBorrar, ct);

            return Ok(new { imagenMenu = urls.FirstOrDefault() });
        }
        
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var uid = GetFirebaseUid();

            var esAdmin = User.IsInRole("admin") || User.Claims.Any(c => c.Type == "role" && c.Value == "admin");

            var ok = await _eliminarRestaurante.HandleAsync(id, uid, esAdmin);
            return ok ? NoContent() : NotFound();
        }


        [HttpGet("buscar")]
        [ProducesResponseType(typeof(RestauranteResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Buscar([FromQuery] string texto, CancellationToken ct)
        {
            var restaurantes = await _buscarRestaurante.HandleAsync(texto, ct);

            var dto = restaurantes.Select(r => new RestauranteResponse
            {
                Id = r.Id,
                Nombre = r.Nombre,
                Latitud = r.Latitud,
                Longitud = r.Longitud,
                Categoria = r.Categoria,
                Rating = r.Rating,
                Direccion = r.Direccion,
                ImagenUrl = string.IsNullOrWhiteSpace(r.ImagenUrl) ? "https://firebasestorage.googleapis.com/v0/b/gustosapp-5c3c9.firebasestorage.app/o/restauranteicono.jpg?alt=media&token=cb818ad4-78b0-4fbb-a13d-46ce1aa66ac1" : r.ImagenUrl
            }).ToList();

            return Ok(dto);
        }


        [Authorize]
        [HttpPost("favorito/{restauranteId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(LimiteFavoritosAlcanzadoException), StatusCodes.Status402PaymentRequired)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]

        public async Task<IActionResult> AgregarFavorito(Guid restauranteId)
        {
            var firebaseUid = GetFirebaseUid();
            await _agregarFavoritoUseCase.HandleAsync(firebaseUid, restauranteId);
            return Ok();
        }

        [HttpDelete("favorito/{restauranteId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> EliminarFavorito(Guid restauranteId)
        {
            var firebaseUid = GetFirebaseUid();
            await _agregarFavoritoUseCase.HandleAsyncDelete(firebaseUid, restauranteId);

            return Ok();

        }

        [HttpGet("{id:guid}/metricas")]
        [ProducesResponseType(typeof(RestauranteMetricasDashboardResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> ObtenerMetricas(
            Guid id,
            CancellationToken ct)
        {
            var usuarioId = await ObtenerUsuarioIdActualAsync(ct);
            var metricas = await _obtenerMetricasRestauranteUseCase.HandleAsync(id, usuarioId, ct);

            var favUsuario = RestauranteMetricasDashboardResponse.ConvertidorDeFavoritos(metricas.TotalFavoritos);

            var rest = new RestauranteMetricasDashboardResponse
            {
                RestauranteId = metricas.RestauranteId,
                TotalTop3Individual = metricas.Estadisticas?.TotalTop3Individual ?? 0,
                TotalTop3Grupo = metricas.Estadisticas?.TotalTop3Grupo ?? 0,
                TotalVisitasPerfil = metricas.Estadisticas?.TotalVisitasPerfil ?? 0,
                FavoritosPorDia = RestauranteMetricasDashboardResponse.CountFavoritosPorDia(favUsuario),
            };

            return Ok(rest);
        }

        private async Task<Guid> ObtenerUsuarioIdActualAsync(CancellationToken ct)
        {
            var firebaseUid = GetFirebaseUid();
            var usuario = await _obtenerUsuario.HandleAsync(FirebaseUid: firebaseUid, ct: ct);
            return usuario.Id;
        }

    }

}
