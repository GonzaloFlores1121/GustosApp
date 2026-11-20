using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;

namespace GustosApp.Application.UseCases.NotificacionUseCases;

public sealed class PublicarNotificacionInformativaUseCase
{
    private readonly INotificacionRepository _notificaciones;
    private readonly INotificacionRealtimeService _realtime;
    private readonly TimeProvider _timeProvider;

    public PublicarNotificacionInformativaUseCase(
        INotificacionRepository notificaciones,
        INotificacionRealtimeService realtime,
        TimeProvider? timeProvider = null)
    {
        _notificaciones = notificaciones;
        _realtime = realtime;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Notificacion> HandleAsync(
        Usuario destinatario,
        string titulo,
        string mensaje,
        TipoNotificacion tipo,
        string claveUnica,
        string? urlDestino,
        CancellationToken ct = default)
    {
        var notificacion = await _notificaciones.CrearSiNoExisteAsync(new Notificacion
        {
            UsuarioDestinoId = destinatario.Id,
            Titulo = titulo,
            Mensaje = mensaje,
            Tipo = tipo,
            ClaveUnica = claveUnica,
            UrlDestino = urlDestino,
            FechaCreacion = _timeProvider.GetUtcNow().UtcDateTime
        }, ct);

        try
        {
            await _realtime.EnviarNotificacionAsync(
                destinatario.FirebaseUid,
                new NotificacionRealtimeMensaje(
                    notificacion.Id,
                    notificacion.Titulo,
                    notificacion.Mensaje,
                    notificacion.Tipo.ToString(),
                    notificacion.Leida,
                    notificacion.FechaCreacion,
                    notificacion.UrlDestino),
                ct);
        }
        catch
        {
            // La notificación persistida se recupera cuando el cliente vuelve a conectar.
        }

        return notificacion;
    }
}
