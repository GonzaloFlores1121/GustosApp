using GustosApp.Application.Interfaces;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;

namespace GustosApp.Application.UseCases.GrupoUseCases.InvitacionGrupoUseCases
{
    public class InvitarUsuarioGrupoUseCase
    {
        private readonly IGrupoRepository _grupoRepository;
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IInvitacionGrupoRepository _invitacionRepository;
        private readonly IMiembroGrupoRepository _miembroGrupoRepository;
        private readonly ISolicitudAmistadRealtimeService _solicitudesRealtime;
        private readonly IApplicationTransaction _transaction;
        private readonly TimeProvider _timeProvider;
        public InvitarUsuarioGrupoUseCase(IGrupoRepository grupoRepository,
            IUsuarioRepository usuarioRepository,
            IInvitacionGrupoRepository invitacionRepository,
            IMiembroGrupoRepository miembroGrupoRepository,
            ISolicitudAmistadRealtimeService solicitudesRealtime,
            IApplicationTransaction? transaction = null,
            TimeProvider? timeProvider = null)
        {
            _grupoRepository = grupoRepository;
            _usuarioRepository = usuarioRepository;
            _invitacionRepository = invitacionRepository;
            _miembroGrupoRepository = miembroGrupoRepository;
            _solicitudesRealtime = solicitudesRealtime;
            _transaction = transaction ?? ImmediateApplicationTransaction.Instance;
            _timeProvider = timeProvider ?? TimeProvider.System;

        }

        public async Task<InvitacionGrupo> HandleAsync(
           string firebaseUid, Guid grupoId, string? EmailUsuario, Guid? UsuarioId,
           string? UsuarioUsername, string? MensajePersonalizado,
           CancellationToken ct = default)
        {
            // 1. Usuario invitador
            var usuarioInvitador = await _usuarioRepository.GetByFirebaseUidAsync(firebaseUid, ct)
                ?? throw new UnauthorizedAccessException("Usuario no encontrado");

            // 2. Grupo
            var grupo = await _grupoRepository.GetByIdAsync(grupoId, ct)
                ?? throw new ArgumentException("Grupo no encontrado");

            // 3. Solo administradores pueden invitar
            if (!await _grupoRepository.UsuarioEsAdministradorAsync(grupoId, usuarioInvitador.Id, ct))
                throw new AccesoProhibidoException("Solo los administradores pueden invitar usuarios");

            // 4. Buscar usuario invitado
            var usuarioInvitado = await ObtenerUsuarioInvitado(EmailUsuario, UsuarioId,
           UsuarioUsername, MensajePersonalizado, ct)
                ?? throw new ArgumentException("No se encontró el usuario a invitar");

            if (usuarioInvitado.Id == usuarioInvitador.Id)
                throw new ArgumentException("No puedes invitarte a ti mismo");

            if (await _miembroGrupoRepository.UsuarioEsMiembroActivoAsync(grupoId, usuarioInvitado.Id, ct))
                throw new ArgumentException("El usuario ya es miembro del grupo");


            await _invitacionRepository.MarcarInvitacionesExpiradasAsync(ct);
            var ultimaInvitacion = await _invitacionRepository.ObtenerUltimaInvitacionAsync(grupoId, usuarioInvitado.Id, ct);

            if (ultimaInvitacion?.Estado == EstadoInvitacion.Pendiente)
                throw new ArgumentException("Ya existe una invitación pendiente para este usuario");

            if (ultimaInvitacion?.Estado == EstadoInvitacion.Aceptada &&
                await _miembroGrupoRepository.UsuarioEsMiembroActivoAsync(grupoId, usuarioInvitado.Id, ct))
                throw new ArgumentException("El usuario ya es miembro del grupo");

            var invitacionCompleta = await _transaction.ExecuteAsync(async transactionCt =>
            {
                var invitacion = new InvitacionGrupo(
                    grupoId,
                    usuarioInvitado.Id,
                    usuarioInvitador.Id,
                    MensajePersonalizado,
                    _timeProvider.GetUtcNow().UtcDateTime);
                await _invitacionRepository.CreateAsync(invitacion, transactionCt);

                return await _invitacionRepository.GetByIdAsync(invitacion.Id, transactionCt)
                    ?? throw new InvalidOperationException("Error al crear la invitación");
            }, ct);

            try
            {
                await _solicitudesRealtime.EnviarInvitacionGrupoAsync(
                    usuarioInvitado.FirebaseUid,
                    invitacionCompleta,
                    ct);
            }
            catch
            {
                // La invitación ya quedó persistida. SignalR es una optimización y el cliente
                // recupera el estado completo al conectar o reconectar.
            }

            return invitacionCompleta;
        }
        private async Task<Usuario?> ObtenerUsuarioInvitado(
         string? emailUsuario,
         Guid? usuarioId,
         string? usuarioUsername,
         string? mensajePersonalizado,
         CancellationToken ct)
        {
            Usuario? usuario = null;

            if (usuarioId.HasValue && usuarioId.Value != Guid.Empty)
            {
                usuario = await _usuarioRepository.GetByIdAsync(usuarioId.Value, ct);
                if (usuario != null) return usuario;
            }

            if (!string.IsNullOrWhiteSpace(usuarioUsername))
            {
                usuario = await _usuarioRepository.GetByUsernameAsync(usuarioUsername.Trim(), ct);
                if (usuario != null) return usuario;
            }

            if (!string.IsNullOrWhiteSpace(emailUsuario))
            {
                usuario = await _usuarioRepository.GetByEmailAsync(emailUsuario.Trim(), ct);
                if (usuario != null) return usuario;
            }

            return null;
        }

    }
}

