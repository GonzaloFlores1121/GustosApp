using System.Security.Claims;
using System.Text.RegularExpressions;
using AutoMapper;
using Azure.Core;
using GustosApp.API.DTO;
using GustosApp.Application.Interfaces;
using GustosApp.Application.UseCases.AmistadUseCases;
using GustosApp.Application.UseCases.GrupoUseCases.InvitacionGrupoUseCases;
using GustosApp.Application.UseCases.NotificacionUseCases;
using GustosApp.Application.UseCases.UsuarioUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Infraestructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using GustosApp.Application.UseCases.GrupoUseCases;

namespace GustosApp.API.Hubs
{
    [Authorize]
    public class NotificacionesHub : Hub
    {
        private readonly ObtenerNotificacionesUsuarioUseCase _obtenerNotificaciones;
        private readonly MarcarNotificacionLeidaUseCase _marcarLeida;
        private readonly ObtenerUsuarioUseCase _obtenerUsuario;
        private readonly IMapper _mapper;
        private readonly IUsuariosActivosService _usuariosActivos;


        public NotificacionesHub(
                ObtenerNotificacionesUsuarioUseCase obtenerNotificaciones,
                MarcarNotificacionLeidaUseCase marcarLeida,
                ObtenerUsuarioUseCase obtenerUsuario,
                IMapper mapper,
                IUsuariosActivosService usuariosActivos)
        {
            _obtenerNotificaciones = obtenerNotificaciones;
            _marcarLeida = marcarLeida;
            _obtenerUsuario = obtenerUsuario;
            _mapper = mapper;
            _usuariosActivos = usuariosActivos;
        }

        // Se ejecuta al conectarse un usuario
        public override async Task OnConnectedAsync()
        {
            try
            {
                var firebaseUid = Context.User?.FindFirst("user_id")?.Value
                 ?? Context.User?.FindFirst("firebase_uid")?.Value
                ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(firebaseUid))
                {
                    Console.WriteLine("⚠️ Usuario conectado sin firebaseUid");
                    await base.OnConnectedAsync();
                    return;
                }

                var usuario = await _obtenerUsuario.HandleAsync(FirebaseUid: firebaseUid, ct: CancellationToken.None);
                if (usuario == null)
                {
                    Console.WriteLine($"⚠️ No se encontró usuario con firebaseUid: {firebaseUid}");
                    await base.OnConnectedAsync();
                    return;
                }

                _usuariosActivos.UsuarioConectado(firebaseUid);

                var notificaciones = await _obtenerNotificaciones.HandleAsync(usuario.Id, CancellationToken.None);

                //DTO
                var notificacionesDTO = _mapper.Map<List<NotificacionDTO>>(notificaciones);

                await Clients.Caller.SendAsync("CargarNotificaciones", notificacionesDTO);
                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en OnConnectedAsync NotificacionesHub: {ex.Message}");
                await base.OnConnectedAsync();
            }
        }


        public async Task MarcarComoLeida(Guid notificacionId)
        {
            var uid = Context.User?.FindFirst("user_id")?.Value
                ?? throw new HubException("No se pudo identificar al usuario autenticado");
            await _marcarLeida.HandleAsync(uid, notificacionId, Context.ConnectionAborted);
            await Clients.User(uid).SendAsync("NotificacionMarcadaLeida", notificacionId);
        }

        public async Task RecargarNotificaciones()
        {
            var uid = Context.User?.FindFirst("user_id")?.Value
                ?? throw new HubException("No se pudo identificar al usuario autenticado");
            var usuario = await _obtenerUsuario.HandleAsync(FirebaseUid: uid, ct: Context.ConnectionAborted)
                ?? throw new HubException("Usuario no encontrado");
            var notificaciones = await _obtenerNotificaciones.HandleAsync(usuario.Id, Context.ConnectionAborted);
            await Clients.Caller.SendAsync(
                "CargarNotificaciones",
                _mapper.Map<List<NotificacionDTO>>(notificaciones),
                Context.ConnectionAborted);
        }
        public override Task OnDisconnectedAsync(Exception? ex)
        {
            var uid = Context.User?.FindFirst("user_id")?.Value;
            if (uid != null)
            {
                _usuariosActivos.UsuarioDesconectado(uid);
            }
            return base.OnDisconnectedAsync(ex);
        }
    }
}
