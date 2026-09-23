using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model.@enum;
using GustosApp.Domain.Model;

namespace GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases
{
    public class AprobarSolicitudRestauranteUseCase
    {
        private readonly IHttpDownloader _downloader;
        private readonly ISolicitudRestauranteRepository _solicitudes;
        private readonly IRestauranteRepository _restaurantes;
        private readonly IUsuarioRepository _usuarios;
        private readonly IOcrService _ocr;
        private readonly IMenuParser _menuParser;
        private readonly IRestriccionRepository _restricciones;
        private readonly IGustoRepository _gustos;
        private readonly IRestauranteMenuRepository _menuRepo;
        private readonly IFirebaseAuthService _firebase;
        private readonly IEmailService _email;
        private readonly IEmailTemplateService _templates;


        public AprobarSolicitudRestauranteUseCase(ISolicitudRestauranteRepository solicitudes,
             IRestauranteRepository restaurantes, IUsuarioRepository usuarios,
             IOcrService ocr, IMenuParser menuParser, IRestriccionRepository restricciones,
            IGustoRepository gustos, IRestauranteMenuRepository menuRepo, 
            IFirebaseAuthService firebase, IEmailService email, 
            IEmailTemplateService templates, IHttpDownloader downloader

            )
        {
            _solicitudes = solicitudes;
            _restaurantes = restaurantes;
            _usuarios = usuarios;
            _ocr = ocr;
            _menuParser = menuParser;
            _restricciones = restricciones;
            _gustos = gustos;
            _menuRepo = menuRepo;
            _firebase = firebase;
            _email = email;
            _templates = templates;
            _downloader = downloader;
        }


        public async Task<Restaurante> HandleAsync(Guid solicitudId, CancellationToken ct)
        {
            var solicitud = await _solicitudes.GetByIdAsync(solicitudId, ct)
                ?? throw new KeyNotFoundException("Solicitud no encontrada");

            if (solicitud.Estado == EstadoSolicitudRestaurante.Aprobada)
            {
                var aprobado = solicitud.RestauranteAprobadoId.HasValue
                    ? await _restaurantes.GetRestauranteConImagenesAsync(solicitud.RestauranteAprobadoId.Value, ct)
                    : null;
                if (aprobado == null)
                    throw new InvalidOperationException("La solicitud ya fue aprobada y no tiene un restaurante vinculado disponible.");
                await SincronizarAprobacionAsync(solicitud, aprobado, ct);
                return aprobado;
            }

            if (solicitud.Estado != EstadoSolicitudRestaurante.Pendiente)
                throw new InvalidOperationException("Solo se pueden aprobar solicitudes pendientes.");
            if (solicitud.Usuario.Rol == RolUsuario.DuenoRestaurante)
                throw new InvalidOperationException("El solicitante ya tiene un restaurante afiliado.");

            var restaurante = solicitud.RestauranteExistenteId.HasValue
                ? await VincularRestauranteAsync(solicitud, ct)
                : await CrearRestauranteDesdeSolicitud(solicitud, ct);

            solicitud.Usuario.Rol = RolUsuario.DuenoRestaurante;
            solicitud.Estado = EstadoSolicitudRestaurante.Aprobada;
            solicitud.RestauranteAprobadoId = restaurante.Id;

            // Una sola escritura confirma la ficha, el propietario y la solicitud.
            await _restaurantes.SaveChangesAsync(ct);

            if (!solicitud.RestauranteExistenteId.HasValue)
            {
                await ProcesarMenuOCR(solicitud, restaurante, ct);
                await _restaurantes.SaveChangesAsync(ct);
            }
            await SincronizarAprobacionAsync(solicitud, restaurante, ct);
            return restaurante;
        }

        private async Task<Restaurante> VincularRestauranteAsync(SolicitudRestaurante solicitud, CancellationToken ct)
        {
            var restaurante = await _restaurantes.GetRestauranteConImagenesAsync(solicitud.RestauranteExistenteId!.Value, ct)
                ?? throw new KeyNotFoundException("Restaurante no encontrado");
            if (restaurante.DuenoId.HasValue || !string.IsNullOrWhiteSpace(restaurante.PropietarioUid))
                throw new InvalidOperationException("El restaurante ya tiene propietario.");
            restaurante.DuenoId = solicitud.UsuarioId;
            restaurante.PropietarioUid = solicitud.UsuarioId.ToString();
            restaurante.ActualizadoUtc = DateTime.UtcNow;
            return restaurante;
        }

        private async Task SincronizarAprobacionAsync(SolicitudRestaurante solicitud, Restaurante restaurante, CancellationToken ct)
        {
            // Si falla un servicio externo, repetir la aprobación retoma estos pasos sin crear otra ficha.
            if (!solicitud.RolFirebaseSincronizado)
            {
                await _firebase.SetUserRoleAsync(solicitud.Usuario.FirebaseUid, RolUsuario.DuenoRestaurante.ToString());
                solicitud.RolFirebaseSincronizado = true;
                await _restaurantes.SaveChangesAsync(ct);
            }
            if (solicitud.CorreoAprobacionEnviado) return;

            //modifcar para deploy
            await _email.EnviarEmailAsync(
                solicitud.Usuario.Email,
                "Tu solicitud fue aprobada",
             _templates.Render("SolicitudAprobada.html", new Dictionary<string, string>
              {
             { "USUARIO", solicitud.Usuario.Nombre },
            { "NOMBRE", restaurante.Nombre },
             { "LINK", $"http://localhost:3000/restaurante/{restaurante.Id}/dashboard" }
             }), ct
            );


            solicitud.CorreoAprobacionEnviado = true;
            await _restaurantes.SaveChangesAsync(ct);
        }

        public async Task ReprocesarMenuAsync(Guid solicitudId, CancellationToken ct)
        {
            var solicitud = await _solicitudes.GetByIdAsync(solicitudId, ct)
                ?? throw new KeyNotFoundException("Solicitud no encontrada");
            if (solicitud.Estado != EstadoSolicitudRestaurante.Aprobada || !solicitud.RestauranteAprobadoId.HasValue)
                throw new InvalidOperationException("La solicitud debe estar aprobada y vinculada a un restaurante.");
            if (!solicitud.Imagenes.Any(i => i.Tipo == TipoImagenSolicitud.Menu))
                throw new InvalidOperationException("La solicitud no tiene imagen de menú.");
            var restaurante = await _restaurantes.GetRestauranteConImagenesAsync(solicitud.RestauranteAprobadoId.Value, ct)
                ?? throw new KeyNotFoundException("Restaurante no encontrado");
            await ProcesarMenuOCR(solicitud, restaurante, ct);
            await _restaurantes.SaveChangesAsync(ct);
        }



        private async Task<Restaurante> CrearRestauranteDesdeSolicitud(
        SolicitudRestaurante solicitud,
        CancellationToken ct)
        {
            var restaurante = new Restaurante
            {
                Id = Guid.NewGuid(),
                PropietarioUid= solicitud.UsuarioId.ToString(),
                DuenoId = solicitud.UsuarioId,
                Nombre = solicitud.Nombre,
                NombreNormalizado = solicitud.Nombre.ToLower().Trim(),
                Direccion = solicitud.Direccion,
                Latitud = solicitud.Latitud ?? 0,
                Longitud = solicitud.Longitud ?? 0,
                PrimaryType = "Restaurante",
                TypesJson = "",
                HorariosJson = solicitud.HorariosJson ?? "{}",
                CreadoUtc = DateTime.UtcNow,
                ActualizadoUtc = DateTime.UtcNow,
                WebUrl = solicitud.WebsiteUrl,
                Rating = null,
            };

            // Relaciones
            restaurante.SetGustos(await _gustos.GetByIdsAsync(solicitud.GustosIds, ct));
            restaurante.SetRestricciones(await _restricciones.GetRestriccionesByIdsAsync(solicitud.RestriccionesIds, ct));

            // Imagen principal y logo
            restaurante.ImagenUrl = solicitud.Imagenes.FirstOrDefault(i => i.Tipo == TipoImagenSolicitud.Destacada)?.Url;
            restaurante.LogoUrl = solicitud.Imagenes.FirstOrDefault(i => i.Tipo == TipoImagenSolicitud.Logo)?.Url;

            // Img multiples
            restaurante.Imagenes = solicitud.Imagenes
                .Where(i => i.Tipo == TipoImagenSolicitud.Interior || i.Tipo == TipoImagenSolicitud.Comida)
                .Select(i => new RestauranteImagen
                {
                    RestauranteId = restaurante.Id,
                    Tipo = i.Tipo == TipoImagenSolicitud.Interior
                        ? TipoImagenRestaurante.Interior
                        : TipoImagenRestaurante.Comida,
                    Url = i.Url,
                    FechaCreacionUtc = DateTime.UtcNow
                })
                .ToList();

            await _restaurantes.AddAsync(restaurante, ct);

            return restaurante;
        }




        private async Task ProcesarMenuOCR(
     SolicitudRestaurante solicitud,
     Restaurante restaurante,
     CancellationToken ct)
        {
            var imgMenu = solicitud.Imagenes
                .FirstOrDefault(i => i.Tipo == TipoImagenSolicitud.Menu);

            if (imgMenu == null) { 
                restaurante.MenuProcesado = false;
            restaurante.MenuError = "Menú vacío";
            return;

        }
            try
            {
                var bytes = await _downloader.DownloadAsync(imgMenu.Url, ct);

                using var stream = new MemoryStream(bytes);

                var texto = await _ocr.ReconocerTextoAsync(new[] { stream }, "spa+eng", ct);

                if (string.IsNullOrWhiteSpace(texto))
                {
                    restaurante.MenuProcesado = false;
                    restaurante.MenuError = "Menú vacío";
                    return;
                }

                var menuJson = await _menuParser.ParsearAsync(texto, "ARS", ct);

                var existente = await _menuRepo.GetByRestauranteIdAsync(restaurante.Id, ct);

                if (existente == null)
                {
                    await _menuRepo.AddAsync(new RestauranteMenu
                    {
                        RestauranteId = restaurante.Id,
                        Moneda = "ARS",
                        Json = menuJson,
                        Version = 1,
                        FechaActualizacionUtc = DateTime.UtcNow
                    }, ct);
                }
                else
                {
                    existente.Json = menuJson;
                    existente.Version++;
                    existente.FechaActualizacionUtc = DateTime.UtcNow;

                    await _menuRepo.UpdateAsync(existente, ct);
                }

                restaurante.MenuProcesado = true;
                restaurante.MenuError = null;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                restaurante.MenuProcesado = false;
                restaurante.MenuError = ex.Message;
            }
        }

    }
}
