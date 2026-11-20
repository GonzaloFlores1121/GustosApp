using GustosApp.API.Hubs.GustosApp.API.Hubs;
using GustosApp.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace GustosApp.API.Hubs.Services;

public class SignalRChatRealtimeService : IChatRealTimeService
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IChatConnectionRegistry _conexiones;

    public SignalRChatRealtimeService(
        IHubContext<ChatHub> hubContext,
        IChatConnectionRegistry conexiones)
    {
        _hubContext = hubContext;
        _conexiones = conexiones;
    }

    public Task UsuarioSeUnio(Guid grupoId, Guid usuarioId, string nombre, string? fotourl) =>
        _hubContext.Clients.Group(ChatHub.Canal(grupoId))
            .SendAsync("UsuarioSeUnio", new { usuarioId, nombre, fotourl });

    public async Task UsuarioExpulsadoDelGrupo(
        Guid grupoId,
        Guid usuarioId,
        string firebaseUid,
        string nombre)
    {
        await RevocarAccesoAsync(
            grupoId,
            firebaseUid,
            "KickedFromGroup",
            new { grupoId, nombreGrupo = nombre });

        await _hubContext.Clients.Group(ChatHub.Canal(grupoId))
            .SendAsync("UsuarioExpulsado", new { usuarioId, firebaseUid, nombre });
        await PublicarPresenciaAsync(grupoId);
    }

    public async Task UsuarioAbandono(
        Guid grupoId,
        Guid usuarioId,
        string nombre,
        string firebaseUid)
    {
        await RevocarAccesoAsync(
            grupoId,
            firebaseUid,
            "GroupAccessRevoked",
            new { grupoId, motivo = "abandono" });

        await _hubContext.Clients.Group(ChatHub.Canal(grupoId))
            .SendAsync("UsuarioAbandonoGrupo", new { usuarioId, nombre, firebaseUid });
        await PublicarPresenciaAsync(grupoId);
    }

    private Task RevocarAccesoAsync(
        Guid grupoId,
        string firebaseUid,
        string evento,
        object payload) =>
        _conexiones.EnGrupoAsync(grupoId, async () =>
        {
            var conexiones = _conexiones.ObtenerConexiones(firebaseUid, grupoId);
            if (conexiones.Count == 0) return;

            await _hubContext.Clients.Clients(conexiones).SendAsync(evento, payload);
            foreach (var connectionId in conexiones)
            {
                await _hubContext.Groups.RemoveFromGroupAsync(
                    connectionId,
                    ChatHub.Canal(grupoId));
                _conexiones.QuitarGrupo(connectionId, grupoId);
            }
        });

    private Task PublicarPresenciaAsync(Guid grupoId) =>
        _hubContext.Clients.Group(ChatHub.Canal(grupoId)).SendAsync(
            "UsuariosConectados",
            new PresenciaChatDto(grupoId, _conexiones.ObtenerUsuarios(grupoId)));
}
