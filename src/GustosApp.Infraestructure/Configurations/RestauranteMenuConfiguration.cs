using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GustosApp.Infraestructure.Configurations
{
    public class RestauranteMenuConfiguration : IEntityTypeConfiguration<RestauranteMenu>
    {
        public void Configure(EntityTypeBuilder<RestauranteMenu> b)
        {
            b.ToTable("RestauranteMenus");
            b.HasKey(x => x.Id);
            b.Property(x => x.Moneda).HasMaxLength(8).IsRequired();
            b.Property(x => x.Json).IsRequired();

            b.HasOne(x => x.Restaurante)
             .WithOne(r => r.Menu)
            .HasForeignKey<RestauranteMenu>(x => x.RestauranteId)
              .OnDelete(DeleteBehavior.Cascade);


         

            b.HasIndex(x => x.RestauranteId).HasDatabaseName("IX_RestMenu_RestauranteId");

            b.Property(x => x.Version).IsRequired();
            b.Property(x => x.FechaActualizacionUtc).IsRequired();
            b.Property(x => x.ImagenFuentePublicadaUrl).HasMaxLength(2048);
            b.Property(x => x.BorradorImagenFuenteUrl).HasMaxLength(2048);
            b.Property(x => x.BorradorError).HasMaxLength(1000);
            b.Property(x => x.EstadoBorrador)
                .HasConversion<string>()
                .HasMaxLength(32)
                .HasDefaultValue(EstadoBorradorMenu.Ninguno)
                .IsRequired();
        }
    }
}
