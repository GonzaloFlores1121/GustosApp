using GustosApp.Application.Interfaces;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;

namespace GustosApp.Application.UseCases.GrupoUseCases.InvitacionGrupoUseCases
{
    public class AceptarInvitacionGrupoUseCase
    {
        private readonly IInvitacionGrupoRepository _invitacionRepository;
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IMiembroGrupoRepository _miembroGrupoRepository;
        private readonly IGustosGrupoRepository _gustosGrupoRepository;
        private readonly ISolicitudAmistadRealtimeService _solicitudesRealtime;
        private readonly IChatRealTimeService _chatRealtime;
        private readonly IGrupoRepository _grupoRepository;
        private readonly IApplicationTransaction _transaction;
        private readonly TimeProvider _timeProvider;
     


        public AceptarInvitacionGrupoUseCase(IInvitacionGrupoRepository invitacionRepository,
            IUsuarioRepository usuarioRepository,
            IMiembroGrupoRepository miembroGrupoRepository,
            IGustosGrupoRepository gustosGrupoRepository,
            ISolicitudAmistadRealtimeService solicitudesRealtime,
            IChatRealTimeService chatRealTimeService,
            IGrupoRepository grupoRepository,
            IApplicationTransaction? transaction = null,
            TimeProvider? timeProvider = null
            )
        {
            _invitacionRepository = invitacionRepository;
            _usuarioRepository = usuarioRepository;
            _miembroGrupoRepository = miembroGrupoRepository;
            _gustosGrupoRepository = gustosGrupoRepository;
            _solicitudesRealtime = solicitudesRealtime;
            _chatRealtime = chatRealTimeService;
            _grupoRepository = grupoRepository;
            _transaction = transaction ?? ImmediateApplicationTransaction.Instance;
            _timeProvider = timeProvider ?? TimeProvider.System;
          
        }

        public async Task<Grupo> HandleAsync(string firebaseUid, Guid invitacionId, CancellationToken cancellationToken = default)
        {
            // Obtener el usuario
            var usuario = await _usuarioRepository.GetByFirebaseUidAsync(firebaseUid, cancellationToken);
            if (usuario == null)
                throw new UnauthorizedAccessException("Usuario no encontrado");

            // Obtener la invitación
            var invitacion = await _invitacionRepository.GetByIdAsync(invitacionId, cancellationToken);
            if (invitacion == null)
                throw new ArgumentException("Invitación no encontrada");

            // Verificar que la invitación es para este usuario
            if (invitacion.UsuarioInvitadoId != usuario.Id)
                throw new AccesoProhibidoException("Esta invitación no es para ti");

            // Verificar que la invitación está pendiente
            if (invitacion.Estado != EstadoInvitacion.Pendiente)
                throw new ArgumentException("Esta invitación ya fue procesada");

            // Verificar que no ha expirado
            var ahora = _timeProvider.GetUtcNow().UtcDateTime;
            if (invitacion.EstaExpirada(ahora))
            {
                invitacion.MarcarComoExpirada(ahora);
                await _invitacionRepository.UpdateAsync(invitacion, cancellationToken);
                throw new ArgumentException("La invitación ha expirado");
            }

            // Verificar que el usuario no es ya miembro activo del grupo
            if (await _miembroGrupoRepository.UsuarioEsMiembroActivoAsync(invitacion.GrupoId, usuario.Id, cancellationToken))
                throw new ArgumentException("Ya eres miembro de este grupo");

            await _transaction.ExecuteAsync(async transactionCt =>
            {
                invitacion.Aceptar(ahora);
                await _invitacionRepository.UpdateAsync(invitacion, transactionCt);

                var miembroExistente = await _miembroGrupoRepository.GetByGrupoYUsuarioAsync(
                    invitacion.GrupoId,
                    usuario.IdUsuario,
                    transactionCt);

                if (miembroExistente != null)
                {
                    miembroExistente.Reincorporar();
                    await _miembroGrupoRepository.UpdateAsync(miembroExistente, transactionCt);
                }
                else
                {
                    var miembro = new MiembroGrupo(invitacion.GrupoId, usuario.Id, false);
                    await _miembroGrupoRepository.CreateAsync(miembro, transactionCt);
                    await _gustosGrupoRepository.AgregarGustosAlGrupo(
                        invitacion.GrupoId,
                        usuario.Gustos.ToList(),
                        miembro.Id);
                }

                return true;
            }, cancellationToken);
            // Recupera el grupo actualizado con relaciones
            var grupo = invitacion.Grupo ?? await _grupoRepository.GetByIdAsync
                (invitacion.GrupoId, cancellationToken)
              
                ?? throw new InvalidOperationException("Error al aceptar la invitación");


            try
            {
                await _solicitudesRealtime.EnviarSolicitudResueltaAsync(
                    usuario.FirebaseUid,
                    invitacion.Id,
                    "invitacion_grupo",
                    "aceptada",
                    cancellationToken);
                await _chatRealtime.UsuarioSeUnio(
                    invitacion.GrupoId,
                    usuario.Id,
                    usuario.IdUsuario,
                    usuario.FotoPerfilUrl);
            }
            catch
            {
                // El estado persistido se recupera al reconectar aunque falle SignalR.
            }

            return grupo;
        }
    }
}
