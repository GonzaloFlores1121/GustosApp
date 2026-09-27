using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Infraestructure;
using Microsoft.EntityFrameworkCore;

namespace GustosApp.Infraestructure.Repositories
{
    public class ChatRepositoryEF : IChatRepository
    {
        private readonly GustosDbContext _context;

        public ChatRepositoryEF(GustosDbContext context) => _context = context;

        public async Task<IReadOnlyList<ChatMensaje>> GetMessagesByGrupoIdAsync(
            Guid grupoId,
            DateTime? antesDeFecha,
            Guid? antesDeId,
            int limite,
            CancellationToken cancellationToken = default)
        {
            var query = _context.ChatMessages
                .AsNoTracking()
                .Where(m => m.GrupoId == grupoId)
                .Include(m => m.Usuario)
                .AsQueryable();

            if (antesDeFecha.HasValue && antesDeId.HasValue)
            {
                query = query.Where(m =>
                    m.FechaEnvio < antesDeFecha.Value ||
                    (m.FechaEnvio == antesDeFecha.Value && m.Id.CompareTo(antesDeId.Value) < 0));
            }

            return await query
                .OrderByDescending(m => m.FechaEnvio)
                .ThenByDescending(m => m.Id)
                .Take(limite)
                .OrderBy(m => m.FechaEnvio)
                .ThenBy(m => m.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ChatMensaje>> GetMessagesAfterAsync(
            Guid grupoId,
            DateTime despuesDeFecha,
            Guid despuesDeId,
            int limite,
            CancellationToken cancellationToken = default)
        {
            return await _context.ChatMessages
                .AsNoTracking()
                .Where(m => m.GrupoId == grupoId &&
                    (m.FechaEnvio > despuesDeFecha ||
                     (m.FechaEnvio == despuesDeFecha && m.Id.CompareTo(despuesDeId) > 0)))
                .Include(m => m.Usuario)
                .OrderBy(m => m.FechaEnvio)
                .ThenBy(m => m.Id)
                .Take(limite)
                .ToListAsync(cancellationToken);
        }

        public Task<ChatMensaje?> GetByOperacionIdAsync(
            Guid grupoId,
            Guid usuarioId,
            Guid operacionId,
            CancellationToken cancellationToken = default) =>
            _context.ChatMessages
                .AsNoTracking()
                .Include(m => m.Usuario)
                .SingleOrDefaultAsync(m =>
                    m.GrupoId == grupoId &&
                    m.UsuarioId == usuarioId &&
                    m.OperacionId == operacionId,
                    cancellationToken);

        public async Task<ChatMensaje> AddMessageAsync(ChatMensaje message, CancellationToken cancellationToken = default)
        {
            _context.ChatMessages.Add(message);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return message;
            }
            catch (DbUpdateException)
            {
                _context.Entry(message).State = EntityState.Detached;
                var existente = await GetByOperacionIdAsync(
                    message.GrupoId,
                    message.UsuarioId,
                    message.OperacionId,
                    cancellationToken);
                if (existente != null) return existente;
                throw;
            }
        }

       

    }
}
