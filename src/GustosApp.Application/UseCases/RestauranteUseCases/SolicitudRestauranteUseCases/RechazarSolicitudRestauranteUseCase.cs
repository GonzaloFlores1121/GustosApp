using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using GustosApp.Application.UseCases.NotificacionUseCases;

namespace GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases
{
    public class RechazarSolicitudRestauranteUseCase
    {
        private readonly ISolicitudRestauranteRepository _solicitudes;
        private readonly IFileStorageService _firebase;
        private readonly IEmailService _email;
        private readonly IEmailTemplateService _templates;
        private readonly PublicarNotificacionInformativaUseCase? _publicarNotificacion;

        public RechazarSolicitudRestauranteUseCase(
            ISolicitudRestauranteRepository solicitudes,
            IFileStorageService firebase,
            IEmailService email,
            IEmailTemplateService templates,
            PublicarNotificacionInformativaUseCase? publicarNotificacion = null)
        {
            _solicitudes = solicitudes;
            _firebase = firebase;
            _email = email;
            _templates = templates;
            _publicarNotificacion = publicarNotificacion;
        }

        public async Task HandleAsync(Guid solicitudId, string motivo, CancellationToken ct = default)
        {
            var solicitud = await _solicitudes.GetByIdAsync(solicitudId, ct)
                ?? throw new Exception("Solicitud no encontrada");

            if (solicitud.Estado != EstadoSolicitudRestaurante.Pendiente
                && solicitud.Estado != EstadoSolicitudRestaurante.Rechazada)
                throw new InvalidOperationException("Solo se pueden rechazar solicitudes pendientes.");

            if (string.IsNullOrWhiteSpace(motivo))
                throw new ArgumentException("Debe especificar un motivo de rechazo.");

            // Cambiar el estado
            if (solicitud.Estado == EstadoSolicitudRestaurante.Pendiente)
            {
                solicitud.Estado = EstadoSolicitudRestaurante.Rechazada;
                solicitud.MotivoRechazo = motivo;

                await _solicitudes.UpdateAsync(solicitud, ct);
            }

            if (_publicarNotificacion != null)
            {
                await _publicarNotificacion.HandleAsync(
                    solicitud.Usuario,
                    "Solicitud de restaurante rechazada",
                    $"Tu solicitud para {solicitud.Nombre} fue rechazada. Revisá el motivo y volvé a intentarlo.",
                    TipoNotificacion.SolicitudRestauranteRechazada,
                    $"solicitud-restaurante:{solicitud.Id}:rechazada",
                    "/restaurante/mi-solicitud",
                    ct);
            }

            //eliminar imagnees de firebase :D
            foreach (var img in solicitud.Imagenes)
            {
                await _firebase.DeleteFileAsync(img.Url);
            }

            if (solicitud.CorreoRechazoEnviado) return;

            await _email.EnviarEmailAsync(
                 solicitud.Usuario.Email,
                 "Tu solicitud fue rechazada",
             _templates.Render("SolicitudRechazada.html", new Dictionary<string, string>
                 {
                { "USUARIO", solicitud.Usuario.Nombre },
                { "NOMBRE", solicitud.Nombre},
                { "MOTIVO", solicitud.MotivoRechazo }
               })
            );

            solicitud.CorreoRechazoEnviado = true;
            await _solicitudes.UpdateAsync(solicitud, ct);


        }
    }

}
