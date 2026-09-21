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

        public CrearSolicitudRestauranteUseCase(
            ISolicitudRestauranteRepository solicitudes,
             IRestriccionRepository restricciones, IGustoRepository gustos,
            IUsuarioRepository usuarios,
            IEmailService email, IEmailTemplateService templates,
            IFileStorageService firebaseStorage
)
        {
            _solicitudes = solicitudes;
            _usuarios = usuarios;
            _gustos = gustos;
            _restricciones = restricciones;
            _email = email;
           _templates = templates;
           _firebaseStorage = firebaseStorage;
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
             CancellationToken ct = default)
        {
            // 1) Obtener usuario
            var usuario = await _usuarios.GetByFirebaseUidAsync(firebaseUid, ct);
            if (usuario == null)
                throw new Exception("Usuario no encontrado");

            // 2) Validar rol
            if (usuario.Rol != RolUsuario.Usuario)
                throw new Exception("Ya sos dueño de un restaurante.");
            if (await _solicitudes.BuscarPendientePorUsuarioAsync(usuario.Id, ct) != null)
                throw new InvalidOperationException("Ya tenés una solicitud de restaurante pendiente.");

            // Parsear lat/lng
            double? latitud = null;
            double? longitud = null;
            if (!string.IsNullOrWhiteSpace(latitudStr))
            {
                var latStr = latitudStr.Replace(",", ".");
                latitud = double.Parse(latStr, CultureInfo.InvariantCulture);
            }
            if (!string.IsNullOrWhiteSpace(longitudStr))
            {
                var lngStr = longitudStr.Replace(",", ".");
                longitud = double.Parse(lngStr, CultureInfo.InvariantCulture);
            }

            var imagenes = new List<SolicitudRestauranteImagen>();
            var urlsSubidas = new List<string>();
            var solicitudGuardada = false;

            try
            {
                if (imagenDestacada != null)
                    imagenes.Add(await SubirImagenAsync(imagenDestacada, TipoImagenSolicitud.Destacada, urlsSubidas));

                if (imagenesInterior != null)
                    foreach (var file in imagenesInterior)
                        imagenes.Add(await SubirImagenAsync(file, TipoImagenSolicitud.Interior, urlsSubidas));

                if (imagenesComidas != null)
                    foreach (var file in imagenesComidas)
                        imagenes.Add(await SubirImagenAsync(file, TipoImagenSolicitud.Comida, urlsSubidas));

                if (imagenMenu != null)
                    imagenes.Add(await SubirImagenAsync(imagenMenu, TipoImagenSolicitud.Menu, urlsSubidas));

                if (logo != null)
                    imagenes.Add(await SubirImagenAsync(logo, TipoImagenSolicitud.Logo, urlsSubidas));

                // 3) Crear la solicitud
                var solicitud = new SolicitudRestaurante
                {
                    UsuarioId = usuario.Id,
                    Nombre = nombre.Trim(),
                    Direccion = direccion.Trim(),
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

                solicitud.Gustos = await _gustos.GetByIdsAsync(gustosIds, ct);
                solicitud.Restricciones = await _restricciones.GetRestriccionesByIdsAsync(restriccionesIds, ct);

                solicitud.Usuario = usuario;
                await _solicitudes.AddAsync(solicitud, ct);
                solicitudGuardada = true;

                // Modificar esto si es deploy?
                await _email.EnviarEmailAsync(
                    "gonzalomarcos551@gmail.com",
                    "Nueva solicitud de restaurante",
                    _templates.Render("SolicitudNueva.html", new Dictionary<string, string>
                    {
                        { "USUARIO", usuario.Email },
                        { "NOMBRE", solicitud.Nombre },
                        { "DIRECCION", solicitud.Direccion },
                        { "LINK", "http://localhost:3000/admin" }
                    })
                );

                return solicitud.Id;
            }
            catch (Exception)
            {
                foreach (var url in solicitudGuardada ? Enumerable.Empty<string>() : urlsSubidas)
                {
                    try { await _firebaseStorage.DeleteFileAsync(url); }
                    catch { }
                }
                throw;
            }
        }

        private async Task<SolicitudRestauranteImagen> SubirImagenAsync(ArchivoEntrada archivo, TipoImagenSolicitud tipo, List<string> urlsSubidas)
        {
            var url = await _firebaseStorage.UploadFileAsync(archivo.Stream, archivo.FileName, "solicitudes");
            urlsSubidas.Add(url);
            return new SolicitudRestauranteImagen
            {
                Tipo = tipo,
                Url = url
            };
        }
    }
}
