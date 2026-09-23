using GustosApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GustosApp.Infraestructure.Configurations
{
    public class VotacionParticipanteConfiguration : IEntityTypeConfiguration<VotacionParticipante>
    {
        public void Configure(EntityTypeBuilder<VotacionParticipante> builder)
        {
            builder.ToTable("VotacionParticipantes");

            builder.HasKey(p => new { p.VotacionId, p.UsuarioId });

            builder.HasOne(p => p.Votacion)
                .WithMany(v => v.Participantes)
                .HasForeignKey(p => p.VotacionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(p => p.Usuario)
                .WithMany()
                .HasForeignKey(p => p.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
