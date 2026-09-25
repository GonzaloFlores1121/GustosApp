using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GustosApp.Application.Interfaces
{
    public sealed record NotificacionRealtimeMensaje(
        Guid Id,
        string Titulo,
        string Mensaje,
        string Tipo,
        bool Leida,
        DateTime FechaCreacion,
        string? UrlDestino);

    public interface INotificacionRealtimeService
    {
        Task EnviarNotificacionAsync(
          string usuarioDestinoId,
          NotificacionRealtimeMensaje notificacion,
          CancellationToken ct);
    }
}
