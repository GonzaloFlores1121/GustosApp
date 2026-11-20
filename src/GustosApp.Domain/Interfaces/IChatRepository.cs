using GustosApp.Domain.Model;

namespace GustosApp.Domain.Interfaces
{
    public interface IChatRepository
    {
        Task<IReadOnlyList<ChatMensaje>> GetMessagesByGrupoIdAsync(
            Guid grupoId,
            DateTime? antesDeFecha,
            Guid? antesDeId,
            int limite,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ChatMensaje>> GetMessagesAfterAsync(
            Guid grupoId,
            DateTime despuesDeFecha,
            Guid despuesDeId,
            int limite,
            CancellationToken cancellationToken = default);
        Task<ChatMensaje?> GetByOperacionIdAsync(
            Guid grupoId,
            Guid usuarioId,
            Guid operacionId,
            CancellationToken cancellationToken = default);
        Task<ChatMensaje> AddMessageAsync(ChatMensaje message, CancellationToken cancellationToken = default);

    

    }
}
