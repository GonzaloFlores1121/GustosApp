namespace GustosApp.Domain.Model
{
    public class VotacionParticipante
    {
        public Guid VotacionId { get; private set; }
        public Guid UsuarioId { get; private set; }

        public VotacionGrupo Votacion { get; private set; } = null!;
        public Usuario Usuario { get; private set; } = null!;

        private VotacionParticipante() { }

        public VotacionParticipante(Guid votacionId, Guid usuarioId)
        {
            if (votacionId == Guid.Empty)
                throw new ArgumentException("La votación es obligatoria.", nameof(votacionId));

            if (usuarioId == Guid.Empty)
                throw new ArgumentException("El usuario es obligatorio.", nameof(usuarioId));

            VotacionId = votacionId;
            UsuarioId = usuarioId;
        }
    }
}
