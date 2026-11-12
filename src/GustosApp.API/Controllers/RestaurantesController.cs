using AutoMapper;
using GustosApp.API.DTO;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.RestauranteUseCases;
using GustosApp.Application.UseCases.UsuarioUseCases;
using GustosApp.Domain.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
        private readonly ObtenerMiSolicitudRestauranteUseCase _obtenerMiSolicitudRestaurante;
        private readonly GestionarBorradorMenuRestauranteUseCase _gestionarBorradorMenu;

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
    ObtenerRestauranteIdPorPropietarioUseCase obtenerRestauranteIdPorPropietario,
    ObtenerMiSolicitudRestauranteUseCase obtenerMiSolicitudRestaurante,
    GestionarBorradorMenuRestauranteUseCase gestionarBorradorMenu)
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
            _obtenerMiSolicitudRestaurante = obtenerMiSolicitudRestaurante;
            _gestionarBorradorMenu = gestionarBorradorMenu;
        }

        [HttpGet("mi-solicitud")]
        [ProducesResponseType(typeof(MiSolicitudRestauranteDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> ObtenerMiSolicitud(CancellationToken ct)
        {
            var solicitud = await _obtenerMiSolicitudRestaurante.HandleAsync(GetFirebaseUid(), ct);
            if (solicitud == null)
                return NoContent();

            return Ok(new MiSolicitudRestauranteDto
            {
                Id = solicitud.Id,
                NombreRestaurante = solicitud.Nombre,
                Direccion = solicitud.Direccion,
                Estado = solicitud.Estado.ToString(),
                Tipo = solicitud.RestauranteExistenteId.HasValue ? "Reclamo" : "Alta",
                MotivoRechazo = solicitud.MotivoRechazo,
                RestauranteAprobadoId = solicitud.RestauranteAprobadoId,
                FechaCreacionUtc = solicitud.FechaCreacion
            });
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
                estado = response.Count == 0 ? "SinCoincidencias" : "Encontrados",
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
        [RequestSizeLimit(10 * 1024 * 1024)]
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
                    dto.WebsiteUrl, ct, dto.DeclaroOtroLocal);

                return Ok(response);
            }
            catch (SolicitudRestaurantePendienteException ex)
            {
                return Conflict(new { code = "SOLICITUD_PENDIENTE", message = ex.Message });
            }
            catch (DbUpdateException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("coincidencias-alta")]
        [EnableRateLimiting("BusquedaRestaurantes")]
        public async Task<IActionResult> CoincidenciasAlta(
            [FromQuery] string? nombre, [FromQuery] string? direccion, [FromQuery] double? latitud,
            [FromQuery] double? longitud, [FromServices] BuscarCoincidenciasAltaUseCase buscar, CancellationToken ct)
        {
            if (nombre?.Length > 160 || direccion?.Length > 300)
                return BadRequest(new { message = "Revisá el nombre y la dirección." });
            return Ok(await buscar.HandleAsync(nombre, direccion, latitud, longitud, ct));
        }


        [HttpPost("{id:guid}/reclamo")]
        [EnableRateLimiting("ReclamosRestaurante")]
        [RequestSizeLimit(3 * 1024 * 1024)]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ReclamarRestaurante(Guid id,
            [FromForm] ReclamarRestauranteDto datos,
            [FromServices] ReclamarRestauranteUseCase casoDeUso, CancellationToken ct)
        {
            if (datos.Comprobante.Length > DatosReclamo.LimiteBytes)
                return BadRequest(new { message = "El comprobante debe pesar hasta 2 MB." });
            using var contenido = new MemoryStream();
            await datos.Comprobante.CopyToAsync(contenido, ct);
            var reclamo = new DatosReclamo(datos.NombreSolicitante, datos.RelacionRestaurante,
                datos.TelefonoContacto, datos.DeclaraAutorizacion, contenido.ToArray(), datos.Comprobante.FileName);
            return Ok(await casoDeUso.HandleAsync(GetFirebaseUid(), id, ct, reclamo));
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
                ct,
                dto.WebUrlFueEnviado);

            var detalle = _mapper.Map<RestauranteDetalleDto>(restauranteActualizado);
            return Ok(detalle);
        }

        [Authorize(Policy = "DuenoRestaurante")]
        [HttpPost("{id:guid}/restricciones/sugerencias")]
        public async Task<IActionResult> SugerirRestricciones(
            Guid id, [FromBody] SugerirRestriccionesOfertaRequest request,
            [FromServices] SugerirRestriccionesOfertaUseCase useCase, CancellationToken ct)
        {
            try
            {
                var resultado = await useCase.HandleAsync(id, await ObtenerUsuarioIdActualAsync(ct),
                    request.GustoIds ?? [], ct);
                return Ok(resultado);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
            catch (AccesoProhibidoException) { return Forbid(); }
            catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        }

        [Authorize(Policy = "DuenoRestaurante")]
        [HttpPost("{id:guid}/confirmar-oferta")]
        public async Task<IActionResult> ConfirmarOferta(
            Guid id, [FromBody] ConfirmarVigenciaRestauranteRequest request,
            [FromServices] ConfirmarVigenciaRestauranteUseCase useCase, CancellationToken ct)
        {
            try
            {
                var actualizado = await useCase.ConfirmarOfertaAsync(id, request.VersionEsperada,
                    await ObtenerUsuarioIdActualAsync(ct), false, ct);
                return Ok(_mapper.Map<RestauranteDetalleDto>(actualizado));
            }
            catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
            catch (AccesoProhibidoException) { return Forbid(); }
            catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
            catch (DbUpdateConcurrencyException) { return Conflict(new { error = "La oferta cambió; recargá la ficha antes de confirmarla." }); }
        }

        [Authorize(Policy = "DuenoRestaurante")]
        [HttpPost("{id:guid}/confirmar-restricciones")]
        public async Task<IActionResult> ConfirmarRestricciones(
            Guid id, [FromBody] ConfirmarVigenciaRestauranteRequest request,
            [FromServices] ConfirmarVigenciaRestauranteUseCase useCase, CancellationToken ct)
        {
            try
            {
                var actualizado = await useCase.ConfirmarRestriccionesAsync(id, request.VersionEsperada,
                    await ObtenerUsuarioIdActualAsync(ct), false, ct);
                return Ok(_mapper.Map<RestauranteDetalleDto>(actualizado));
            }
            catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
            catch (AccesoProhibidoException) { return Forbid(); }
            catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
            catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Las declaraciones cambiaron; recargá la ficha antes de confirmarlas." }); }
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
        [RequestSizeLimit(10 * 1024 * 1024)]
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
        [RequestSizeLimit(10 * 1024 * 1024)]
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
        [HttpPost("{id:guid}/imagenes/interior")]
        [RequestSizeLimit(30 * 1024 * 1024)]
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
            if (request.SoloBorrar) return BadRequest(new { error = "Elegí la foto que querés eliminar." });
            var archivos = request.Archivos?.Select(a => new ArchivoEntrada(a.OpenReadStream(), a.FileName)).ToList();
            var imagenes = await _actualizarImagenesUseCase.AgregarImagenesAsync(
                id, usuarioId, TipoImagenRestaurante.Interior, archivos ?? [], ct);
            return Ok(RespuestaGaleria(imagenes));
        }


        [Authorize(Policy = "DuenoRestaurante")]
        [HttpPut("{id:guid}/imagenes/comidas")]
        [HttpPost("{id:guid}/imagenes/comidas")]
        [RequestSizeLimit(30 * 1024 * 1024)]
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
            if (request.SoloBorrar) return BadRequest(new { error = "Elegí la foto que querés eliminar." });
            var archivos = request.Archivos?.Select(a => new ArchivoEntrada(a.OpenReadStream(), a.FileName)).ToList();
            var imagenes = await _actualizarImagenesUseCase.AgregarImagenesAsync(
                id, usuarioId, TipoImagenRestaurante.Comida, archivos ?? [], ct);
            return Ok(RespuestaGaleria(imagenes));
        }

        [Authorize(Policy = "DuenoRestaurante")]
        [HttpPut("{id:guid}/imagenes/{categoria}/{imagenId:guid}")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ReemplazarFoto(Guid id, string categoria, Guid imagenId,
            [FromForm] ActualizarImagenRestauranteRequest request, CancellationToken ct)
        {
            if (request.Archivo is null || request.SoloBorrar) return BadRequest(new { error = "Elegí una foto para reemplazar." });
            var tipo = TipoGaleria(categoria);
            var usuarioId = await ObtenerUsuarioIdActualAsync(ct);
            var imagenes = await _actualizarImagenesUseCase.ModificarImagenAsync(id, usuarioId, tipo, imagenId,
                new ArchivoEntrada(request.Archivo.OpenReadStream(), request.Archivo.FileName), ct);
            return Ok(RespuestaGaleria(imagenes));
        }

        [Authorize(Policy = "DuenoRestaurante")]
        [HttpDelete("{id:guid}/imagenes/{categoria}/{imagenId:guid}")]
        public async Task<IActionResult> EliminarFoto(Guid id, string categoria, Guid imagenId, CancellationToken ct)
        {
            var tipo = TipoGaleria(categoria);
            var usuarioId = await ObtenerUsuarioIdActualAsync(ct);
            var imagenes = await _actualizarImagenesUseCase.ModificarImagenAsync(id, usuarioId, tipo, imagenId, null, ct);
            return Ok(RespuestaGaleria(imagenes));
        }

        private static TipoImagenRestaurante TipoGaleria(string categoria) => categoria switch
        {
            "interior" => TipoImagenRestaurante.Interior,
            "comidas" => TipoImagenRestaurante.Comida,
            _ => throw new ArgumentException("La categoría de fotos no es válida.")
        };

        private static object RespuestaGaleria(IEnumerable<RestauranteImagen> imagenes) => new
        {
            imagenesInterior = imagenes.Where(i => i.Tipo == TipoImagenRestaurante.Interior).Select(i => i.Url).ToList(),
            imagenesComida = imagenes.Where(i => i.Tipo == TipoImagenRestaurante.Comida).Select(i => i.Url).ToList(),
            imagenesGaleria = imagenes.Select(i => new ImagenGaleriaDto { Id = i.Id, Url = i.Url, Tipo = (int)i.Tipo, Orden = i.Orden, Origen = i.Origen.ToString() }).ToList()
        };


        [Authorize(Policy = "DuenoRestaurante")]
        [HttpPut("{id:guid}/imagenes/menu")]
        [RequestSizeLimit(10 * 1024 * 1024)]
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
            if (request.SoloBorrar)
                return BadRequest(new { code = "BORRADOR_TOKEN_REQUERIDO", message = "Usá la acción Descartar borrador para quitar una carga sin afectar el menú publicado." });
            if (request.Archivo == null)
                return BadRequest(new { message = "Seleccioná una imagen de menú." });

            var borrador = await _gestionarBorradorMenu.CrearDesdeArchivoAsync(
                id,
                usuarioId,
                new ArchivoEntrada(request.Archivo.OpenReadStream(), request.Archivo.FileName),
                ct);

            return Ok(CrearRespuestaBorrador(borrador, borrador.Version > 0));
        }

        [Authorize(Policy = "DuenoRestaurante")]
        [HttpGet("{id:guid}/menu/borrador")]
        public async Task<IActionResult> ObtenerBorradorMenu(Guid id, CancellationToken ct)
        {
            var usuarioId = await ObtenerUsuarioIdActualAsync(ct);
            var menu = await _gestionarBorradorMenu.ObtenerAsync(id, usuarioId, ct);
            return Ok(menu == null
                ? new BorradorMenuRestauranteResponse()
                : CrearRespuestaBorrador(menu, menu.Version > 0));
        }

        [Authorize(Policy = "DuenoRestaurante")]
        [HttpPut("{id:guid}/menu/borrador/publicar")]
        public async Task<IActionResult> PublicarBorradorMenu(
            Guid id,
            [FromBody] PublicarBorradorMenuRequest request,
            CancellationToken ct)
        {
            var usuarioId = await ObtenerUsuarioIdActualAsync(ct);
            await _gestionarBorradorMenu.PublicarAsync(
                id, usuarioId, request.Token, request.JsonRevisado, ct);
            var menu = await _gestionarBorradorMenu.ObtenerAsync(id, usuarioId, ct);
            return Ok(CrearRespuestaBorrador(menu!, true));
        }

        [Authorize(Policy = "DuenoRestaurante")]
        [HttpDelete("{id:guid}/menu/borrador/{token:guid}")]
        public async Task<IActionResult> DescartarBorradorMenu(
            Guid id,
            Guid token,
            CancellationToken ct)
        {
            var usuarioId = await ObtenerUsuarioIdActualAsync(ct);
            await _gestionarBorradorMenu.DescartarAsync(id, usuarioId, token, ct);
            return NoContent();
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
        [EnableRateLimiting("BusquedaRestaurantes")]
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
                ImagenUrl = ImagenRestaurantePublica.Url(r.Id, r.ImagenUrl)
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

        [Authorize(Policy = "DuenoRestaurante")]
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

        private static BorradorMenuRestauranteResponse CrearRespuestaBorrador(
            RestauranteMenu menu,
            bool tieneMenuPublicado) => new()
        {
            Token = menu.BorradorToken,
            Estado = menu.EstadoBorrador.ToString(),
            Json = menu.BorradorJson,
            ImagenFuenteUrl = menu.BorradorImagenFuenteUrl,
            ImagenPublicadaUrl = menu.ImagenFuentePublicadaUrl,
            Error = menu.BorradorError,
            FechaUtc = menu.FechaBorradorUtc,
            TieneMenuPublicado = tieneMenuPublicado,
            VersionPublicada = menu.Version
        };

        private async Task<Guid> ObtenerUsuarioIdActualAsync(CancellationToken ct)
        {
            var firebaseUid = GetFirebaseUid();
            var usuario = await _obtenerUsuario.HandleAsync(FirebaseUid: firebaseUid, ct: ct);
            return usuario.Id;
        }

    }

}
