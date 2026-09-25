using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Domain.Model;

namespace GustosApp.Application.Interfaces
{
    public interface ISolicitudAmistadRealtimeService
    {
        Task EnviarSolicitudAsync(
            string destinatarioUid,
            SolicitudAmistad solicitud,
            CancellationToken ct);

        Task EnviarInvitacionGrupoAsync(
            string destinatarioUid,
            InvitacionGrupo invitacion,
            CancellationToken ct);

        Task EnviarSolicitudResueltaAsync(
            string destinatarioUid,
            Guid solicitudId,
            string tipo,
            string estado,
            CancellationToken ct);
    }

}
