using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Domain.Model.@enum;

namespace GustosApp.Domain.Model
{
    public class SolicitudRestaurante
    {
        public Guid Id { get; set; }
        public bool Archivada { get; private set; }
        public int VersionArchivo { get; private set; }
        public DateTime? FechaCambioArchivoUtc { get; private set; }

        public bool CambiarArchivo(bool archivada, int versionEsperada, DateTime fechaUtc)
        {
            if (Estado != EstadoSolicitudRestaurante.Rechazada)
                throw new InvalidOperationException("Solo se pueden archivar o restaurar solicitudes rechazadas.");
            if (Archivada == archivada) return false;
            if (VersionArchivo != versionEsperada)
                throw new InvalidOperationException("El archivo cambió. Recargá la solicitud antes de continuar.");
            Archivada = archivada;
            FechaCambioArchivoUtc = fechaUtc;
            VersionArchivo++;
            return true;
        }

        // Nulo para altas nuevas; informado para reclamar una ficha existente.
        public Guid? RestauranteExistenteId { get; set; }
        public bool DeclaroOtroLocal { get; set; }
        public Guid? RestauranteAprobadoId { get; set; }
        public bool RolFirebaseSincronizado { get; set; }
        public bool CorreoAprobacionEnviado { get; set; }
        public bool CorreoRechazoEnviado { get; set; }
        public bool CorreoNuevaSolicitudEnviado { get; set; }
        public EstadoCorreoResolucion CorreoResolucion { get; set; }
        public EstadoMenuResolucion MenuResolucion { get; set; }
        public Guid? TokenMenuResolucion { get; set; }
        public bool NotificacionResolucionPublicada { get; set; }
        public bool ArchivosRechazoEliminados { get; set; }
        public string? NombreSolicitante { get; set; }
        public string? RelacionRestaurante { get; set; }
        public string? TelefonoContacto { get; set; }
        public bool DeclaraAutorizacion { get; set; }
        public string? TipoComprobante { get; set; }
        public byte[]? ComprobanteReclamo { get; set; }

        // Usuario que hace la solicitud
        public Guid UsuarioId { get; set; }
        public Usuario Usuario { get; set; }

        // Datos básicos del restaurante
        public string Nombre { get; set; } = default!;
        public string Direccion { get; set; } = default!;
        public double? Latitud { get; set; }
        public double? Longitud { get; set; }
        public string WebsiteUrl { get; set; }

        // Horarios enviados del front (JSON)
        public string? HorariosJson { get; set; }

        public string? MotivoRechazo { get; set; }

        // Campos de negocio
        public List<Guid> GustosIds { get; set; } = new List<Guid>();
        public List<Guid> RestriccionesIds { get; set; } = new List<Guid>();

        public ICollection<Gusto> Gustos { get; set; } = new List <Gusto>();
        public ICollection<Restriccion> Restricciones { get; set; } =new List <Restriccion>();


        // Estado de la solicitud
        public EstadoSolicitudRestaurante Estado { get; set; } = EstadoSolicitudRestaurante.Pendiente;

        // Fecha de creación
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        // Imágenes asociadas a esta solicitud
        public ICollection<SolicitudRestauranteImagen> Imagenes { get; set; }
            = new List<SolicitudRestauranteImagen>();
    }

}
