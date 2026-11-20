using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GustosApp.Infraestructure.Migrations;

[DbContext(typeof(GustosDbContext))]
[Migration("20261120050000_CrearVentanasVistaRestaurante")]
public sealed class CrearVentanasVistaRestaurante : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Estado operativo de deduplicación, consultado solo por SQL parametrizado.
        migrationBuilder.Sql("""
            CREATE TABLE dbo.VentanasVistaRestaurante (
                RestauranteId uniqueidentifier NOT NULL REFERENCES dbo.Restaurantes(Id) ON DELETE CASCADE,
                UsuarioId uniqueidentifier NOT NULL REFERENCES dbo.Usuarios(Id) ON DELETE CASCADE,
                FechaUtc datetime2 NOT NULL,
                CONSTRAINT PK_VentanasVistaRestaurante PRIMARY KEY (RestauranteId, UsuarioId)
            );
            CREATE INDEX IX_VentanasVistaRestaurante_FechaUtc ON dbo.VentanasVistaRestaurante (FechaUtc);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql("DROP TABLE dbo.VentanasVistaRestaurante;");
}
