using System.Text.Json;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Configuration;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.NotificacionUseCases;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases;

public sealed record ResumenResolucionSolicitud(string Menu, string Correo, bool AccesoPendiente,
    bool NotificacionPendiente, bool PuedeReintentar)
{
    public static ResumenResolucionSolicitud Desde(SolicitudRestaurante solicitud)
    {
        var correo = solicitud.CorreoAprobacionEnviado || solicitud.CorreoRechazoEnviado
            ? EstadoCorreoResolucion.Aceptado : solicitud.CorreoResolucion;
        var accesoPendiente = solicitud.Estado == EstadoSolicitudRestaurante.Aprobada
            && solicitud.RestauranteAprobadoId.HasValue && !solicitud.RolFirebaseSincronizado;
        var notificacionPendiente = solicitud.CorreoResolucion != EstadoCorreoResolucion.NoProgramado
            && !solicitud.NotificacionResolucionPublicada;
        return new(solicitud.MenuResolucion.ToString(), correo.ToString(), accesoPendiente, notificacionPendiente,
            accesoPendiente || notificacionPendiente
                || solicitud.MenuResolucion is EstadoMenuResolucion.Pendiente or EstadoMenuResolucion.Error
                || correo is EstadoCorreoResolucion.Pendiente or EstadoCorreoResolucion.Enviando);
    }
}

public sealed class ProcesarResolucionSolicitudUseCase(
    ISolicitudRestauranteRepository solicitudes,
    IRestauranteRepository restaurantes,
    IRestauranteMenuRepository menus,
    IBloqueoResolucionSolicitud bloqueo,
    IHttpDownloader downloader,
    IOcrService ocr,
    IMenuParser parser,
    IFirebaseAuthService firebase,
    IEmailService email,
    IEmailTemplateService plantillas,
    PublicarNotificacionInformativaUseCase? notificaciones = null,
    IOptions<EnlacesAplicacionOptions>? opcionesEnlaces = null,
    ILogger<ProcesarResolucionSolicitudUseCase>? logger = null)
{
    private readonly EnlacesAplicacionOptions _enlaces = opcionesEnlaces?.Value ?? new();

    public async Task<bool> CompletarAsync(Guid solicitudId, CancellationToken ct, bool soloMenu = false)
    {
        try
        {
            return await bloqueo.IntentarAsync(solicitudId, async token =>
            {
                var solicitud = await solicitudes.GetByIdAsync(solicitudId, token);
                if (solicitud == null || solicitud.Estado == EstadoSolicitudRestaurante.Pendiente) return;
                await solicitudes.RecargarAsync(solicitud, token);
                var aprobada = solicitud.Estado == EstadoSolicitudRestaurante.Aprobada;
                var restaurante = aprobada && solicitud.RestauranteAprobadoId is Guid id
                    ? await restaurantes.GetRestauranteByIdAsync(id, token) : null;
                var vinculoVigente = !aprobada || (restaurante != null && restaurante.DuenoId == solicitud.UsuarioId
                    && restaurante.PropietarioUid == solicitud.Usuario.FirebaseUid);

                if (!soloMenu)
                {
                    if (vinculoVigente) await PublicarNotificacionAsync(solicitud, restaurante, token);
                    if (aprobada && vinculoVigente) await SincronizarRolAsync(solicitud, token);
                    await ProcesarCorreoAsync(solicitud, restaurante, vinculoVigente, token);
                }

                if (solicitud.MenuResolucion is EstadoMenuResolucion.Pendiente or EstadoMenuResolucion.Error)
                {
                    if (vinculoVigente && restaurante != null && !solicitud.RestauranteExistenteId.HasValue)
                        await ProcesarMenuAsync(solicitud, restaurante, token);
                    else
                        await ConservarMenuAsync(solicitud, token);
                }
            }, ct);
        }
        catch (Exception ex)
        {
            RegistrarFallo(solicitudId, "resolución", ex);
            return false;
        }
    }

    private async Task PublicarNotificacionAsync(SolicitudRestaurante solicitud, Restaurante? restaurante, CancellationToken ct)
    {
        if (solicitud.NotificacionResolucionPublicada || solicitud.CorreoResolucion == EstadoCorreoResolucion.NoProgramado)
            return;

        try
        {
            if (notificaciones != null)
            {
                var aprobada = restaurante != null;
                await notificaciones.HandleAsync(solicitud.Usuario,
                    aprobada ? "Solicitud de restaurante aprobada" : "Solicitud de restaurante rechazada",
                    aprobada ? $"Tu solicitud para administrar {restaurante!.Nombre} fue aprobada."
                        : $"Tu solicitud para {solicitud.Nombre} fue rechazada. Revisá el motivo y volvé a intentarlo.",
                    aprobada ? TipoNotificacion.SolicitudRestauranteAprobada : TipoNotificacion.SolicitudRestauranteRechazada,
                    $"solicitud-restaurante:{solicitud.Id}:{(aprobada ? "aprobada" : "rechazada")}",
                    aprobada ? $"/restaurante/{restaurante!.Id}/dashboard" : "/restaurante/mi-solicitud", ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RegistrarFallo(solicitud.Id, "notificación", ex);
            return;
        }
        solicitud.NotificacionResolucionPublicada = true;
        await solicitudes.UpdateAsync(solicitud, ct);
    }

    private async Task SincronizarRolAsync(SolicitudRestaurante solicitud, CancellationToken ct)
    {
        if (solicitud.RolFirebaseSincronizado || solicitud.Usuario.Rol != RolUsuario.DuenoRestaurante) return;
        try
        {
            ct.ThrowIfCancellationRequested();
            await firebase.SetUserRoleAsync(solicitud.Usuario.FirebaseUid, RolUsuario.DuenoRestaurante.ToString());
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            RegistrarFallo(solicitud.Id, "rol", ex);
            return;
        }
        solicitud.RolFirebaseSincronizado = true;
        await solicitudes.UpdateAsync(solicitud, ct);
    }

    private async Task ProcesarCorreoAsync(SolicitudRestaurante solicitud, Restaurante? restaurante,
        bool vinculoVigente, CancellationToken ct)
    {
        if (solicitud.CorreoAprobacionEnviado || solicitud.CorreoRechazoEnviado) return;
        if (solicitud.CorreoResolucion == EstadoCorreoResolucion.Enviando)
        {
            // Poseer el bloqueo significa que ya no hay otra ejecución local confirmando este envío.
            solicitud.CorreoResolucion = EstadoCorreoResolucion.Incierto;
            await solicitudes.UpdateAsync(solicitud, ct);
            return;
        }
        if (solicitud.CorreoResolucion != EstadoCorreoResolucion.Pendiente || !vinculoVigente) return;

        var aprobada = restaurante != null;
        string html;
        try
        {
            html = plantillas.Render(aprobada ? "SolicitudAprobada.html" : "SolicitudRechazada.html",
                new Dictionary<string, string>
                {
                    ["USUARIO"] = solicitud.Usuario.Nombre,
                    ["NOMBRE"] = restaurante?.Nombre ?? solicitud.Nombre,
                    ["LINK"] = aprobada ? _enlaces.CrearUrl($"/restaurante/{restaurante!.Id}/dashboard") : "",
                    ["MOTIVO"] = solicitud.MotivoRechazo ?? ""
                });
        }
        catch (Exception ex)
        {
            RegistrarFallo(solicitud.Id, "preparación de correo", ex);
            return;
        }

        solicitud.CorreoResolucion = EstadoCorreoResolucion.Enviando;
        await solicitudes.UpdateAsync(solicitud, ct);
        try
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(TimeSpan.FromSeconds(35));
            await email.EnviarEmailAsync(solicitud.Usuario.Email,
                aprobada ? "Tu solicitud fue aprobada" : "Tu solicitud fue rechazada", html, limite.Token);
            solicitud.CorreoResolucion = EstadoCorreoResolucion.Aceptado;
            if (aprobada) solicitud.CorreoAprobacionEnviado = true;
            else solicitud.CorreoRechazoEnviado = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            solicitud.CorreoResolucion = ex is CorreoNoIniciadoException
                ? EstadoCorreoResolucion.Pendiente : EstadoCorreoResolucion.Incierto;
            RegistrarFallo(solicitud.Id, "envío de correo", ex);
        }
        await solicitudes.UpdateAsync(solicitud, ct);
    }

    private async Task ProcesarMenuAsync(SolicitudRestaurante solicitud, Restaurante restaurante, CancellationToken ct)
    {
        var fuente = solicitud.Imagenes.FirstOrDefault(i => i.Tipo == TipoImagenSolicitud.Menu)?.Url;
        var menu = await menus.GetByRestauranteIdAsync(restaurante.Id, ct);
        if (solicitud.TokenMenuResolucion is not Guid token || string.IsNullOrWhiteSpace(fuente) || restaurante.MenuProcesado == true
            || (menu != null && !CorrespondeAlAlta(menu, token, fuente)))
        {
            await ConservarMenuAsync(solicitud, ct);
            return;
        }

        if (menu?.EstadoBorrador == EstadoBorradorMenu.Listo)
        {
            solicitud.MenuResolucion = EstadoMenuResolucion.Listo;
            await solicitudes.UpdateAsync(solicitud, ct);
            return;
        }

        var esNuevo = menu == null;
        menu ??= new RestauranteMenu { RestauranteId = restaurante.Id, Version = 0 };
        // Una ejecución posterior invalida cualquier resultado de un intento que perdió la conexión.
        token = Guid.NewGuid();
        solicitud.TokenMenuResolucion = token;
        solicitud.MenuResolucion = EstadoMenuResolucion.Pendiente;
        menu.IniciarBorrador(token, fuente, DateTime.UtcNow, OrigenImagenRestaurante.Propietario);
        if (esNuevo) await menus.AddAsync(menu, ct);
        else await menus.UpdateAsync(menu, ct);

        string? json = null;
        var mensaje = "No pudimos analizar el menú. Podés reintentar desde administración.";
        try
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(TimeSpan.FromSeconds(90));
            var bytes = await downloader.DownloadAsync(fuente, limite.Token);
            using var stream = new MemoryStream(bytes);
            var texto = await ocr.ReconocerTextoAsync([stream], "spa+eng", limite.Token);
            if (string.IsNullOrWhiteSpace(texto))
                mensaje = "No pudimos detectar texto en la imagen del menú.";
            else
            {
                var candidato = await parser.ParsearAsync(texto, "ARS", limite.Token);
                using var documento = JsonDocument.Parse(candidato);
                if (documento.RootElement.ValueKind == JsonValueKind.Object
                    && documento.RootElement.TryGetProperty("categorias", out var categorias)
                    && categorias.ValueKind == JsonValueKind.Array)
                    json = candidato;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            RegistrarFallo(solicitud.Id, "análisis de menú", ex);
        }

        await menus.ReloadAsync(menu, ct);
        var menuActual = await menus.GetByRestauranteIdAsync(restaurante.Id, ct);
        if (menuActual == null || !CorrespondeAlAlta(menuActual, token, fuente))
        {
            await ConservarMenuAsync(solicitud, ct);
            return;
        }
        if (json != null) menuActual.CompletarBorrador(token, json, DateTime.UtcNow);
        else menuActual.MarcarErrorBorrador(token, mensaje, DateTime.UtcNow);
        solicitud.MenuResolucion = json != null ? EstadoMenuResolucion.Listo : EstadoMenuResolucion.Error;
        // El contexto compartido guarda el borrador y el resultado de la solicitud juntos.
        await menus.UpdateAsync(menuActual, ct);
    }

    private static bool CorrespondeAlAlta(RestauranteMenu menu, Guid token, string fuente) =>
        menu.Version == 0 && menu.ImagenFuentePublicadaUrl == null && menu.BorradorToken == token
        && menu.OrigenImagenBorrador == OrigenImagenRestaurante.Propietario
        && string.Equals(menu.BorradorImagenFuenteUrl, fuente, StringComparison.Ordinal);

    private Task ConservarMenuAsync(SolicitudRestaurante solicitud, CancellationToken ct)
    {
        solicitud.MenuResolucion = EstadoMenuResolucion.Conservado;
        return solicitudes.UpdateAsync(solicitud, ct);
    }

    private void RegistrarFallo(Guid id, string paso, Exception ex) =>
        logger?.LogWarning("Paso {Paso} pendiente de revisión en solicitud {SolicitudId}. Tipo: {Tipo}.",
            paso, id, ex.GetType().Name);
}
