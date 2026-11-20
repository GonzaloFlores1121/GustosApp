using AutoMapper;
using GustosApp.API.DTO;
using GustosApp.Application.UseCases.AmistadUseCases;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Application.UseCases.GrupoUseCases.InvitacionGrupoUseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GustosApp.API.Hubs
{
    [Authorize]
    public class SolicitudesAmistadHub : Hub
    {
        private readonly AceptarSolicitudUseCase _aceptarSolicitud;
        private readonly RechazarSolicitudUseCase _rechazarSolicitud;
        private readonly ObtenerSolicitudesPendientesUseCase _obtenerPendientes;
        private readonly IMapper _mapper;
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly ObtenerInvitacionesUsuarioUseCase _obtenerInvitacionesGrupo;
        public SolicitudesAmistadHub(
            AceptarSolicitudUseCase aceptarSolicitud,
            RechazarSolicitudUseCase rechazarSolicitud,
            ObtenerSolicitudesPendientesUseCase obtenerPendientes,
            ObtenerInvitacionesUsuarioUseCase obtenerInvitacionesGrupo,
            IUsuarioRepository usuarioRepository,
            IMapper mapper)
        {
            _aceptarSolicitud = aceptarSolicitud;
            _rechazarSolicitud = rechazarSolicitud;
            _obtenerPendientes = obtenerPendientes;
            _obtenerInvitacionesGrupo = obtenerInvitacionesGrupo;
            _mapper = mapper;
            _usuarioRepository = usuarioRepository;
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                var uid = Context.User?.FindFirst("user_id")?.Value;
                if (string.IsNullOrEmpty(uid))
                {
                    Console.WriteLine("⚠️ Usuario conectado a SolicitudesAmistadHub sin user_id");
                    await base.OnConnectedAsync();
                    return;
                }

                var solicitudes = await _obtenerPendientes.HandleAsync(uid, CancellationToken.None);
                var solicitudesDTO = _mapper.Map<List<SolicitudAmistadResponse>>(solicitudes);
                var invitacionesGrupo = await _obtenerInvitacionesGrupo.HandleAsync(uid, CancellationToken.None);
                var invitacionesGrupoDTO = _mapper.Map<List<InvitacionGrupoResponse>>(invitacionesGrupo);

                await Clients.Caller.SendAsync("SolicitudesPendientes", solicitudesDTO);
                await Clients.Caller.SendAsync("InvitacionesGrupoPendientes", invitacionesGrupoDTO);
                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en OnConnectedAsync SolicitudesAmistadHub: {ex.Message}");
                await base.OnConnectedAsync();
            }
        }

        public async Task AceptarSolicitud(Guid solicitudId)
        {
            var uid = Context.User?.FindFirst("user_id")?.Value;
            if (uid == null) return;

            var solicitud = await _aceptarSolicitud.HandleAsync(uid, solicitudId, CancellationToken.None);

          
            var usuarioQueAcepto = await _usuarioRepository.GetByIdAsync(solicitud.DestinatarioId, CancellationToken.None);

         
            var emisor = await _usuarioRepository.GetByIdAsync(solicitud.RemitenteId, CancellationToken.None);

            await Clients.User(uid).SendAsync("SolicitudResuelta", new
            {
                Id = solicitudId,
                Tipo = "solicitud_amistad",
                Estado = "aceptada"
            });

            if (emisor?.FirebaseUid != null && usuarioQueAcepto != null)
            {
               
                await Clients.User(emisor.FirebaseUid).SendAsync("SolicitudAceptada", new
                {
                    nombreUsuario = usuarioQueAcepto.Nombre,
                 
                });
            }
        }

        public async Task RechazarSolicitud(Guid solicitudId)
        {

            var uid = Context.User?.FindFirst("user_id")?.Value;

            await _rechazarSolicitud.HandleAsync(uid,solicitudId, CancellationToken.None);
            await Clients.User(uid).SendAsync("SolicitudResuelta", new
            {
                Id = solicitudId,
                Tipo = "solicitud_amistad",
                Estado = "rechazada"
            });
        }

        public async Task RecargarSolicitudes()
        {
            var uid = Context.User?.FindFirst("user_id")?.Value
                ?? throw new HubException("No se pudo identificar al usuario autenticado");
            var solicitudes = await _obtenerPendientes.HandleAsync(uid, Context.ConnectionAborted);
            var invitaciones = await _obtenerInvitacionesGrupo.HandleAsync(uid, Context.ConnectionAborted);
            await Clients.Caller.SendAsync(
                "SolicitudesPendientes",
                _mapper.Map<List<SolicitudAmistadResponse>>(solicitudes),
                Context.ConnectionAborted);
            await Clients.Caller.SendAsync(
                "InvitacionesGrupoPendientes",
                _mapper.Map<List<InvitacionGrupoResponse>>(invitaciones),
                Context.ConnectionAborted);
        }
    }

}
