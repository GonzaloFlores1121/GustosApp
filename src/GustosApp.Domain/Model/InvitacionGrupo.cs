using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GustosApp.Domain.Model
{
   

    public class InvitacionGrupo
    {
        public Guid Id { get; private set; } = Guid.NewGuid();

        public Guid? NotificacionId { get; set; }
        public Guid GrupoId { get;  set; }
        public Guid UsuarioInvitadoId { get;set; }
        public Guid UsuarioInvitadorId { get;  set; }
        public DateTime FechaInvitacion { get; set; } = DateTime.UtcNow;
        public DateTime? FechaRespuesta { get; set; }
        public EstadoInvitacion Estado { get; set; } = EstadoInvitacion.Pendiente;
        public string? MensajePersonalizado { get; set; }
        public DateTime FechaExpiracion { get; set; }
        public byte[] Version { get; private set; } = Array.Empty<byte>();

        // Navegación
        public Grupo Grupo { get; set; }
        public Usuario UsuarioInvitado { get; set; }
        public Usuario UsuarioInvitador { get; set; }

        public Notificacion? Notificacion { get; set; }

        private InvitacionGrupo() { } // Para EF Core

        public InvitacionGrupo(
            Guid grupoId,
            Guid usuarioInvitadoId,
            Guid usuarioInvitadorId,
            string? mensajePersonalizado = null,
            DateTime? fechaCreacionUtc = null)
        {
            var ahora = fechaCreacionUtc ?? DateTime.UtcNow;
            GrupoId = grupoId;
            UsuarioInvitadoId = usuarioInvitadoId;
            UsuarioInvitadorId = usuarioInvitadorId;
            MensajePersonalizado = mensajePersonalizado;
            FechaInvitacion = ahora;
            FechaExpiracion = ahora.AddDays(7);
        }

        public void Aceptar(DateTime? ahoraUtc = null)
        {
            var ahora = ahoraUtc ?? DateTime.UtcNow;
            if (Estado != EstadoInvitacion.Pendiente)
                throw new InvalidOperationException("Solo se pueden aceptar invitaciones pendientes");

            if (EstaExpirada(ahora))
                throw new InvalidOperationException("La invitación ha expirado");

            Estado = EstadoInvitacion.Aceptada;
            FechaRespuesta = ahora;
        }

        public void Rechazar(DateTime? ahoraUtc = null)
        {
            var ahora = ahoraUtc ?? DateTime.UtcNow;
            if (Estado != EstadoInvitacion.Pendiente)
                throw new InvalidOperationException("Solo se pueden rechazar invitaciones pendientes");

            if (EstaExpirada(ahora))
                throw new InvalidOperationException("La invitación ha expirado");

            Estado = EstadoInvitacion.Rechazada;
            FechaRespuesta = ahora;
        }

        public void MarcarComoExpirada(DateTime? ahoraUtc = null)
        {
            var ahora = ahoraUtc ?? DateTime.UtcNow;
            if (Estado == EstadoInvitacion.Pendiente)
            {
                Estado = EstadoInvitacion.Expirada;
                FechaRespuesta = ahora;
            }
        }

        public bool EstaExpirada(DateTime? ahoraUtc = null)
        {
            return FechaExpiracion <= (ahoraUtc ?? DateTime.UtcNow) && Estado == EstadoInvitacion.Pendiente;
        }
    }
}
