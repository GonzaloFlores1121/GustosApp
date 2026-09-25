using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.UseCases.NotificacionUseCases;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Model.@enum;

namespace GustosApp.Application.UseCases.AmistadUseCases
{
    public class AceptarSolicitudUseCase
    {
        private readonly ISolicitudAmistadRepository _solicitudRepository;
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IMiembroGrupoRepository _miembroGrupoRepository;
        private readonly PublicarNotificacionInformativaUseCase? _publicarNotificacion;
        private readonly IApplicationTransaction _transaction;

        public AceptarSolicitudUseCase(
            ISolicitudAmistadRepository solicitudRepository,
            IUsuarioRepository usuarioRepository,
            IMiembroGrupoRepository miembroGrupoRepository,
            PublicarNotificacionInformativaUseCase? publicarNotificacion = null,
            IApplicationTransaction? transaction = null)
        {
            _solicitudRepository = solicitudRepository;
            _usuarioRepository = usuarioRepository;
            _miembroGrupoRepository = miembroGrupoRepository;
            _publicarNotificacion = publicarNotificacion;
            _transaction = transaction ?? ImmediateApplicationTransaction.Instance;
        }

        public async Task<SolicitudAmistad> HandleAsync(
        string firebaseUid,
        Guid solicitudId,
        CancellationToken ct = default)
        {
            var usuario = await _usuarioRepository.GetByFirebaseUidAsync(firebaseUid, ct)
                ?? throw new UnauthorizedAccessException("Usuario no encontrado");

            var solicitud = await _solicitudRepository.GetByIdAsync(solicitudId, ct)
                ?? throw new ArgumentException("Solicitud no encontrada");

            if (solicitud.DestinatarioId != usuario.Id)
                throw new AccesoProhibidoException("Solo el destinatario puede aceptar la solicitud");

            await _transaction.ExecuteAsync(async transactionCt =>
            {
                solicitud.Aceptar();
                await _solicitudRepository.UpdateAsync(solicitud, transactionCt);

                if (_publicarNotificacion != null)
                {
                    await _publicarNotificacion.HandleAsync(
                        solicitud.Remitente,
                        "Solicitud de amistad aceptada",
                        $"{usuario.Nombre} aceptó tu solicitud de amistad.",
                        TipoNotificacion.AmistadAceptada,
                        $"amistad:{solicitud.Id}:aceptada",
                        $"/perfil/{usuario.IdUsuario}",
                        transactionCt);
                }

                return true;
            }, ct);


            return solicitud;
        }

    }
}
