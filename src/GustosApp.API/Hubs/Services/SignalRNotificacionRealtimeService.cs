using GustosApp.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace GustosApp.API.Hubs.Services
{
    public class SignalRNotificacionRealtimeService : INotificacionRealtimeService
    {
        private readonly IHubContext<NotificacionesHub> _hubContext;

        public SignalRNotificacionRealtimeService(IHubContext<NotificacionesHub> hubContext)
        {
            _hubContext = hubContext;
        }

    public async Task EnviarNotificacionAsync(
    string firebaseUid,
    NotificacionRealtimeMensaje notificacion,
    CancellationToken ct)
        {
            await _hubContext.Clients.User(firebaseUid)
                .SendAsync("RecibirNotificacion", notificacion, ct);
        }
    }
    }
