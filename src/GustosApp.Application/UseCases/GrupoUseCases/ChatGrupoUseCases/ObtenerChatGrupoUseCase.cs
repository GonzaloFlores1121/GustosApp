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
    public sealed record PaginaMensajesChat(
        IReadOnlyList<ChatMensaje> Mensajes,
        bool HayMas);

    public class ObtenerChatGrupoUseCase
    {
        private readonly IChatRepository _chatRepository;
        private readonly IGrupoRepository _grupoRepository;

        public ObtenerChatGrupoUseCase(IChatRepository chatRepository, IGrupoRepository grupoRepository)
        {
            _chatRepository = chatRepository;
            _grupoRepository = grupoRepository;
        }

        public async Task<PaginaMensajesChat> HandleAsync(
            string firebaseUid,
            Guid grupoId,
            DateTime? antesDeFecha = null,
            Guid? antesDeId = null,
            int limite = 50,
            CancellationToken cancellationToken = default)
        {
            await ValidarMembresiaAsync(firebaseUid, grupoId, cancellationToken);
            ValidarCursor(antesDeFecha, antesDeId);
            var limiteSeguro = Math.Clamp(limite, 1, 50);
            var mensajes = await _chatRepository.GetMessagesByGrupoIdAsync(
                grupoId,
                antesDeFecha,
                antesDeId,
                limiteSeguro + 1,
                cancellationToken);
            var hayMas = mensajes.Count > limiteSeguro;
            return new PaginaMensajesChat(
                mensajes.Skip(hayMas ? 1 : 0).ToList(),
                hayMas);
        }

        public async Task<PaginaMensajesChat> ObtenerPosterioresAsync(
            string firebaseUid,
            Guid grupoId,
            DateTime despuesDeFecha,
            Guid despuesDeId,
            int limite = 50,
            CancellationToken cancellationToken = default)
        {
            await ValidarMembresiaAsync(firebaseUid, grupoId, cancellationToken);
            if (despuesDeId == Guid.Empty)
                throw new ArgumentException("El cursor del chat no es válido.");
            var limiteSeguro = Math.Clamp(limite, 1, 50);
            var mensajes = await _chatRepository.GetMessagesAfterAsync(
                grupoId,
                despuesDeFecha,
                despuesDeId,
                limiteSeguro + 1,
                cancellationToken);
            var hayMas = mensajes.Count > limiteSeguro;
            return new PaginaMensajesChat(
                mensajes.Take(limiteSeguro).ToList(),
                hayMas);
        }

        private async Task ValidarMembresiaAsync(
            string firebaseUid,
            Guid grupoId,
            CancellationToken cancellationToken)
        {
            var grupo = await _grupoRepository.GetByIdAsync(grupoId, cancellationToken);
            if (grupo == null)
                throw new ArgumentException("Grupo no encontrado");

            var esMiembro = grupo.Miembros.Any(m =>
                m.Activo && m.Usuario.FirebaseUid == firebaseUid);
            if (!esMiembro)
                throw new AccesoProhibidoException("No pertenece a este grupo");
        }

        private static void ValidarCursor(DateTime? fecha, Guid? id)
        {
            if (fecha.HasValue != id.HasValue || id == Guid.Empty)
                throw new ArgumentException("El cursor del chat no es válido.");
        }
    }


}
