using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Application.Common.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GustosApp.Application.UseCases.NotificacionUseCases
{
    public class MarcarNotificacionLeidaUseCase
    {
        private readonly INotificacionRepository _repository;
        private readonly IUsuarioRepository _usuarios;

        public MarcarNotificacionLeidaUseCase(
            INotificacionRepository repository,
            IUsuarioRepository usuarios)
        {
            _repository = repository;
            _usuarios = usuarios;
        }

        public async Task HandleAsync(string firebaseUid, Guid notificacionId, CancellationToken ct)
        {
            if (notificacionId == Guid.Empty)
                throw new ArgumentException("El ID de notificación no puede ser vacío.");

            var usuario = await _usuarios.GetByFirebaseUidAsync(firebaseUid, ct)
                ?? throw new UnauthorizedAccessException("Usuario no encontrado");
            var notificacion = await _repository.GetByIdAsync(notificacionId, ct);
            if (notificacion.UsuarioDestinoId != usuario.Id)
                throw new AccesoProhibidoException("La notificación no pertenece al usuario autenticado");

            await _repository.MarcarComoLeidaAsync(notificacionId, ct);
        }

    }
}
