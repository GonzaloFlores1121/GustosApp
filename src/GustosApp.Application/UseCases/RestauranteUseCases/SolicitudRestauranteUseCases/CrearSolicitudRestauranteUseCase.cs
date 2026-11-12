using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model.@enum;
using GustosApp.Domain.Model;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Common;
using System.Globalization;
using GustosApp.Application.Configuration;
using GustosApp.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases
{
    public class CrearSolicitudRestauranteUseCase
    {
        private readonly ISolicitudRestauranteRepository _solicitudes;
        private readonly IGustoRepository _gustos;
        private readonly IRestriccionRepository _restricciones;
        private readonly IUsuarioRepository _usuarios;
        private readonly IEmailService _email;
        private readonly IEmailTemplateService _templates;
        private readonly IFileStorageService _firebaseStorage;
        private readonly EnlacesAplicacionOptions _enlaces;
        private readonly ILogger<CrearSolicitudRestauranteUseCase>? _logger;

        public CrearSolicitudRestauranteUseCase(
            ISolicitudRestauranteRepository solicitudes,
             IRestriccionRepository restricciones, IGustoRepository gustos,
            IUsuarioRepository usuarios,
            IEmailService email, IEmailTemplateService templates,
            IFileStorageService firebaseStorage,
            IOptions<EnlacesAplicacionOptions>? enlaces = null,
            ILogger<CrearSolicitudRestauranteUseCase>? logger = null
)
        {
            _solicitudes = solicitudes;
            _usuarios = usuarios;
            _gustos = gustos;
            _restricciones = restricciones;
            _email = email;
           _templates = templates;
           _firebaseStorage = firebaseStorage;
           _enlaces = enlaces?.Value ?? new EnlacesAplicacionOptions();
           _logger = logger;
        }

        public async Task<Guid> HandleAsync(
             string firebaseUid,
             string nombre,
             string direccion,
             string? latitudStr,
             string? longitudStr,
             string? horariosJson,
             List<Guid>? gustosIds,
             List<Guid>? restriccionesIds,
             ArchivoEntrada? imagenDestacada,
             List<ArchivoEntrada>? imagenesInterior,
             List<ArchivoEntrada>? imagenesComidas,
             ArchivoEntrada? imagenMenu,
             ArchivoEntrada? logo,
             string websiteUrl,
             CancellationToken ct = default,
             bool declaroOtroLocal = false)
        {
            // 1) Obtener usuario
            var usuario = await _usuarios.GetByFirebaseUidAsync(firebaseUid, ct);
            if (usuario == null)
                throw new Exception("Usuario no encontrado");

            // 2) Validar rol
            if (usuario.Rol != RolUsuario.Usuario)
                throw new Exception("Ya sos dueño de un restaurante.");
            // Parsear lat/lng
            double? latitud = null;
            double? longitud = null;
            if (!string.IsNullOrWhiteSpace(latitudStr))
            {
                var latStr = latitudStr.Replace(",", ".");
                latitud = Math.Round(double.Parse(latStr, CultureInfo.InvariantCulture), 7);
            }
            if (!string.IsNullOrWhiteSpace(longitudStr))
            {
                var lngStr = longitudStr.Replace(",", ".");
                longitud = Math.Round(double.Parse(lngStr, CultureInfo.InvariantCulture), 7);
            }

            async Task<Guid?> ReutilizarAltaPendienteAsync(bool notificar)
            {
                var pendiente = await _solicitudes.BuscarPendientePorUsuarioAsync(usuario.Id, ct);
                if (pendiente == null) return null;
                if (pendiente.RestauranteExistenteId.HasValue
                    || BuscarCoincidenciasAltaUseCase.Normalizar(pendiente.Nombre) != BuscarCoincidenciasAltaUseCase.Normalizar(nombre)
                    || BuscarCoincidenciasAltaUseCase.Normalizar(pendiente.Direccion) != BuscarCoincidenciasAltaUseCase.Normalizar(direccion)
                    || pendiente.Latitud != latitud || pendiente.Longitud != longitud)
                    throw new SolicitudRestaurantePendienteException();
                if (notificar) await IntentarNotificarAdministracionAsync(pendiente, usuario, ct);
                return pendiente.Id;
            }

            if (await ReutilizarAltaPendienteAsync(notificar: true) is Guid idPendiente) return idPendiente;

            var imagenes = new List<SolicitudRestauranteImagen>();
            var urlsSubidas = new List<string>();
            var solicitudGuardada = false;
            var solicitudId = Guid.NewGuid();
            var carpetaArchivos = $"solicitudes/{solicitudId:N}";

            try
            {
                if (imagenDestacada != null)
                    imagenes.Add(await SubirImagenAsync(imagenDestacada, TipoImagenSolicitud.Destacada, urlsSubidas, carpetaArchivos));

                if (imagenesInterior != null)
                    foreach (var file in imagenesInterior)
                        imagenes.Add(await SubirImagenAsync(file, TipoImagenSolicitud.Interior, urlsSubidas, carpetaArchivos));

                if (imagenesComidas != null)
                    foreach (var file in imagenesComidas)
                        imagenes.Add(await SubirImagenAsync(file, TipoImagenSolicitud.Comida, urlsSubidas, carpetaArchivos));

                if (imagenMenu != null)
                    imagenes.Add(await SubirImagenAsync(imagenMenu, TipoImagenSolicitud.Menu, urlsSubidas, carpetaArchivos));

                if (logo != null)
                    imagenes.Add(await SubirImagenAsync(logo, TipoImagenSolicitud.Logo, urlsSubidas, carpetaArchivos));

                // 3) Crear la solicitud
                var solicitud = new SolicitudRestaurante
                {
                    Id = solicitudId,
                    UsuarioId = usuario.Id,
                    Nombre = nombre.Trim(),
                    Direccion = direccion.Trim(),
                    DeclaroOtroLocal = declaroOtroLocal && BuscarCoincidenciasAltaUseCase.UbicacionValida(latitud, longitud),
                    Latitud = latitud,
                    Longitud = longitud,
                    HorariosJson = horariosJson,
                    GustosIds = gustosIds ?? new List<Guid>(),
                    RestriccionesIds = restriccionesIds ?? new List<Guid>(),
                    Imagenes = imagenes,
                    FechaCreacion = DateTime.UtcNow,
                    Estado = EstadoSolicitudRestaurante.Pendiente,
                    WebsiteUrl = websiteUrl.Trim()
                };

                solicitud.Gustos = await _gustos.GetByIdsAsync(gustosIds ?? [], ct);
                solicitud.Restricciones = await _restricciones.GetRestriccionesByIdsAsync(restriccionesIds ?? [], ct);

                solicitud.Usuario = usuario;
                await _solicitudes.AddAsync(solicitud, ct);
                solicitudGuardada = true;

                await IntentarNotificarAdministracionAsync(solicitud, usuario, ct);

                return solicitud.Id;
            }
            catch (Exception ex)
            {
                // La capa de almacenamiento comprueba todas las referencias antes de borrar.
                // Así, un commit exitoso con respuesta fallida conserva la solicitud válida.
                if (!solicitudGuardada)
                    foreach (var url in urlsSubidas)
                    {
                        try { await _firebaseStorage.ScheduleCleanupAsync(url, carpetaArchivos); }
                        catch (Exception falloLimpieza) { _logger?.LogWarning(falloLimpieza, "Falló limpieza de archivo de solicitud {SolicitudId}.", solicitudId); }
                    }
                if (ex is SolicitudRestauranteConcurrenteException && await ReutilizarAltaPendienteAsync(notificar: false) is Guid idConcurrente)
                    return idConcurrente;
                throw;
            }
        }

        private async Task IntentarNotificarAdministracionAsync(
            SolicitudRestaurante solicitud,
            Usuario usuario,
            CancellationToken ct)
        {
            if (solicitud.CorreoNuevaSolicitudEnviado ||
                string.IsNullOrWhiteSpace(_enlaces.EmailAdministracion))
                return;

            try
            {
                await _email.EnviarEmailAsync(
                    _enlaces.EmailAdministracion,
                    "Nueva solicitud de restaurante",
                    _templates.Render("SolicitudNueva.html", new Dictionary<string, string>
                    {
                        { "USUARIO", usuario.Email },
                        { "NOMBRE", solicitud.Nombre },
                        { "DIRECCION", solicitud.Direccion },
                        { "LINK", _enlaces.CrearUrl("/admin") }
                    }),
                    ct);
                solicitud.CorreoNuevaSolicitudEnviado = true;
                await _solicitudes.UpdateAsync(solicitud, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogWarning(
                    ex,
                    "La solicitud {SolicitudId} se guardó, pero no se pudo enviar el correo administrativo.",
                    solicitud.Id);
            }
        }

        private async Task<SolicitudRestauranteImagen> SubirImagenAsync(ArchivoEntrada archivo, TipoImagenSolicitud tipo, List<string> urlsSubidas, string carpeta)
        {
            var url = await _firebaseStorage.UploadFileAsync(archivo.Stream, archivo.FileName, carpeta);
            urlsSubidas.Add(url);
            return new SolicitudRestauranteImagen
            {
                Tipo = tipo,
                Url = url
            };
        }
    }
}
