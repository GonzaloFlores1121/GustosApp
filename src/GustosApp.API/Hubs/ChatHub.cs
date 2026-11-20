using GustosApp.API.Hubs.Services;
using GustosApp.Application.UseCases.GrupoUseCases.ChatGrupoUseCases;
using GustosApp.Domain.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GustosApp.API.Hubs.GustosApp.API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly EnviarMensajeGrupoUseCase _enviarMensaje;
    private readonly ObtenerChatGrupoUseCase _obtenerChat;
    private readonly IChatConnectionRegistry _conexiones;

    public ChatHub(
        EnviarMensajeGrupoUseCase enviarMensaje,
        ObtenerChatGrupoUseCase obtenerChat,
        IChatConnectionRegistry conexiones)
    {
        _enviarMensaje = enviarMensaje;
        _obtenerChat = obtenerChat;
        _conexiones = conexiones;
    }

    public async Task JoinGroup(string groupId)
    {
        var uid = ObtenerUid();
        var grupoId = ParsearGrupo(groupId);
        var canal = Canal(grupoId);

        var pagina = await _conexiones.EnGrupoAsync(grupoId, async () =>
        {
            try
            {
                var resultado = await _obtenerChat.HandleAsync(
                    uid, grupoId, cancellationToken: Context.ConnectionAborted);
                await Groups.AddToGroupAsync(Context.ConnectionId, canal, Context.ConnectionAborted);
                _conexiones.RegistrarGrupo(Context.ConnectionId, grupoId);
                return resultado;
            }
            catch
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, canal, CancellationToken.None);
                _conexiones.QuitarGrupo(Context.ConnectionId, grupoId);
                throw;
            }
        });

        await Clients.Caller.SendAsync(
            "LoadChatHistory",
            CrearPagina(pagina, grupoId),
            Context.ConnectionAborted);
        await PublicarPresenciaAsync(grupoId);
    }

    public async Task LeaveGroup(string groupId)
    {
        var grupoId = ParsearGrupo(groupId);
        await _conexiones.EnGrupoAsync(grupoId, async () =>
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                Canal(grupoId),
                Context.ConnectionAborted);
            _conexiones.QuitarGrupo(Context.ConnectionId, grupoId);
        });
        await PublicarPresenciaAsync(grupoId);
    }

    public async Task SendMessageToGroup(string groupId, string mensaje, string operacionId)
    {
        var uid = ObtenerUid();
        var grupoId = ParsearGrupo(groupId);
        if (!Guid.TryParse(operacionId, out var operacion) || operacion == Guid.Empty)
            throw new HubException("El identificador de envío no es válido.");

        var guardado = await _enviarMensaje.HandleAsync(
            uid,
            grupoId,
            mensaje,
            Context.ConnectionAborted,
            operacion);

        await Clients.Group(Canal(grupoId)).SendAsync(
            "ReceiveMessage",
            CrearMensaje(guardado, uid),
            Context.ConnectionAborted);
    }

    public async Task<ChatPaginaDto> LoadOlderMessages(
        string groupId,
        DateTime antesDeFecha,
        string antesDeId)
    {
        var uid = ObtenerUid();
        var grupoId = ParsearGrupo(groupId);
        if (!Guid.TryParse(antesDeId, out var mensajeId) || mensajeId == Guid.Empty)
            throw new HubException("El cursor del chat no es válido.");
        var pagina = await _obtenerChat.HandleAsync(
            uid,
            grupoId,
            antesDeFecha,
            mensajeId,
            cancellationToken: Context.ConnectionAborted);
        return CrearPagina(pagina, grupoId);
    }

    public async Task<ChatPaginaDto> RecoverMessages(
        string groupId,
        DateTime despuesDeFecha,
        string despuesDeId)
    {
        var uid = ObtenerUid();
        var grupoId = ParsearGrupo(groupId);
        if (!Guid.TryParse(despuesDeId, out var mensajeId) || mensajeId == Guid.Empty)
            throw new HubException("El cursor del chat no es válido.");
        var pagina = await _obtenerChat.ObtenerPosterioresAsync(
            uid,
            grupoId,
            despuesDeFecha,
            mensajeId,
            cancellationToken: Context.ConnectionAborted);
        return CrearPagina(pagina, grupoId);
    }

    public override async Task OnConnectedAsync()
    {
        _conexiones.RegistrarConexion(Context.ConnectionId, ObtenerUid());
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var grupos = _conexiones.EliminarConexion(Context.ConnectionId);
        foreach (var grupoId in grupos)
            await PublicarPresenciaAsync(grupoId);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task PublicarPresenciaAsync(Guid grupoId) =>
        await Clients.Group(Canal(grupoId)).SendAsync(
            "UsuariosConectados",
            new PresenciaChatDto(grupoId, _conexiones.ObtenerUsuarios(grupoId)),
            CancellationToken.None);

    private string ObtenerUid() =>
        Context.User?.FindFirst("user_id")?.Value
        ?? throw new HubException("No se pudo identificar al usuario.");

    private static Guid ParsearGrupo(string groupId) =>
        Guid.TryParse(groupId, out var grupoId) && grupoId != Guid.Empty
            ? grupoId
            : throw new HubException("El identificador del grupo no es válido.");

    internal static string Canal(Guid grupoId) => $"chat:{grupoId:D}";

    private static ChatMensajeDto CrearMensaje(ChatMensaje mensaje, string? uid = null) => new(
        mensaje.Id,
        mensaje.GrupoId,
        mensaje.UsuarioNombre,
        mensaje.Mensaje,
        mensaje.FechaEnvio,
        uid ?? mensaje.Usuario?.FirebaseUid);

    private static ChatPaginaDto CrearPagina(PaginaMensajesChat pagina, Guid grupoId) => new(
        grupoId,
        pagina.Mensajes.Select(m => CrearMensaje(m)).ToList(),
        pagina.HayMas);
}

public sealed record ChatMensajeDto(
    Guid Id,
    Guid GrupoId,
    string Usuario,
    string Mensaje,
    DateTime Fecha,
    string? Uid);

public sealed record ChatPaginaDto(
    Guid GrupoId,
    IReadOnlyList<ChatMensajeDto> Mensajes,
    bool HayMas);

public sealed record PresenciaChatDto(
    Guid GrupoId,
    IReadOnlyList<string> Usuarios);
