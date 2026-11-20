using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Application.Common.Exceptions;

namespace GustosApp.Application.UseCases.GrupoUseCases.ChatGrupoUseCases
{
    public class EnviarMensajeGrupoUseCase : IEnviarMensajeGrupoUseCase
    {
        private readonly IChatRepository _chatRepository;
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IGrupoRepository _grupoRepository;


        public EnviarMensajeGrupoUseCase(IChatRepository chatRepository, IUsuarioRepository usuarioRepository,
            IGrupoRepository grupoRepository)
        {
            _chatRepository = chatRepository;
            _usuarioRepository = usuarioRepository;
            _grupoRepository = grupoRepository;
        }

        public async Task<ChatMensaje> HandleAsync(
            string firebaseUid,
            Guid grupoId,
            string mensaje,
            CancellationToken cancellationToken = default,
            Guid? operacionId = null)
        {

            var usuario = await _usuarioRepository.GetByFirebaseUidAsync(firebaseUid, cancellationToken);
            if (usuario == null)
                throw new UnauthorizedAccessException("Usuario no encontrado");


            var grupo = await _grupoRepository.GetByIdAsync(grupoId, cancellationToken);
            if (grupo == null)
                throw new ArgumentException("El grupo no existe.");


            var esMiembro = grupo.Miembros?.Any(m => m.Activo && m.UsuarioId == usuario.Id) ?? false;
            if (!esMiembro)
                throw new AccesoProhibidoException("No pertenecés a este grupo.");


            if (string.IsNullOrWhiteSpace(mensaje))
                throw new ArgumentException("El mensaje no puede estar vacío.");
            var mensajeNormalizado = mensaje.Trim();
            if (mensajeNormalizado.Length > 2000)
                throw new ArgumentException("El mensaje no puede superar los 2000 caracteres.");

            var idOperacion = operacionId.GetValueOrDefault(Guid.NewGuid());
            if (idOperacion == Guid.Empty)
                throw new ArgumentException("El identificador de envío no es válido.");

            var existente = await _chatRepository.GetByOperacionIdAsync(
                grupoId,
                usuario.Id,
                idOperacion,
                cancellationToken);
            if (existente != null) return existente;

            var chat = new ChatMensaje
            {
                Id = Guid.NewGuid(),
                GrupoId = grupoId,
                UsuarioId = usuario.Id,
                UsuarioNombre = $"{usuario.Nombre} {usuario.Apellido}",
                Mensaje = mensajeNormalizado,
                FechaEnvio = DateTime.UtcNow,
                OperacionId = idOperacion
            };

            var saved = await _chatRepository.AddMessageAsync(chat, cancellationToken);
            return saved;
        }
      
    }
}
