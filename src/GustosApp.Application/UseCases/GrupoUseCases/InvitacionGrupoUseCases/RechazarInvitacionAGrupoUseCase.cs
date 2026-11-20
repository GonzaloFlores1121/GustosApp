using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;

namespace GustosApp.Application.UseCases.GrupoUseCases.InvitacionGrupoUseCases
{
    public class RechazarInvitacionAGrupoUseCase
    {

        private readonly IUsuarioRepository _usuarioRepo;
        private readonly IInvitacionGrupoRepository _invitacionRepository;
        private readonly ISolicitudAmistadRealtimeService _solicitudesRealtime;
        private readonly TimeProvider _timeProvider;
       

        public RechazarInvitacionAGrupoUseCase(
            IUsuarioRepository usuarioRepo,
            IInvitacionGrupoRepository invitacionRepository,
            ISolicitudAmistadRealtimeService solicitudesRealtime,
            TimeProvider? timeProvider = null)
        {
            _usuarioRepo = usuarioRepo;
            _invitacionRepository = invitacionRepository;
            _solicitudesRealtime = solicitudesRealtime;
            _timeProvider = timeProvider ?? TimeProvider.System;
          
        }



        public async Task HandleAsync(string uid, Guid invitacionId, CancellationToken ct)
        {
          
            var usuario = await _usuarioRepo.GetByFirebaseUidAsync(uid, ct);
            if (usuario == null)
                throw new UnauthorizedAccessException("Usuario no encontrado");

           
            var invitacion = await _invitacionRepository.GetByIdAsync(invitacionId, ct);
            if (invitacion == null)
                throw new ArgumentException("Invitación no encontrada");


            // Verificar destinatario correcto
            if (invitacion.UsuarioInvitadoId != usuario.Id)
                throw new AccesoProhibidoException("Esta invitación no es para ti");

            // Estado debe ser pendiente
            if (invitacion.Estado != EstadoInvitacion.Pendiente)
                throw new ArgumentException("La invitación ya fue procesada");

            //  Verificar expiración
            var ahora = _timeProvider.GetUtcNow().UtcDateTime;
            if (invitacion.EstaExpirada(ahora))
            {
                invitacion.MarcarComoExpirada(ahora);
                await _invitacionRepository.UpdateAsync(invitacion, ct);
                throw new ArgumentException("La invitación ha expirado");
            }

            // Marcar como rechazada
            invitacion.Rechazar(ahora);
            await _invitacionRepository.UpdateAsync(invitacion, ct);

            try
            {
                await _solicitudesRealtime.EnviarSolicitudResueltaAsync(
                    usuario.FirebaseUid,
                    invitacion.Id,
                    "invitacion_grupo",
                    "rechazada",
                    ct);
            }
            catch
            {
                // El estado se recupera desde la API si SignalR no está disponible.
            }
        }
    }
}
