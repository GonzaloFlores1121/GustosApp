using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GustosApp.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class UnificarOpinionLocalPorUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM [OpinionRestaurante]
                    WHERE [UsuarioId] IS NOT NULL
                    GROUP BY [UsuarioId], [RestauranteId]
                    HAVING COUNT(*) > 1
                )
                    THROW 51000, N'No se puede aplicar la unicidad de reseñas: existen usuarios con más de una reseña local para el mismo restaurante. Revisá esos datos antes de continuar.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_OpinionRestaurante_UsuarioId",
                table: "OpinionRestaurante");

            migrationBuilder.CreateIndex(
                name: "IX_OpinionRestaurante_UsuarioId_RestauranteId",
                table: "OpinionRestaurante",
                columns: new[] { "UsuarioId", "RestauranteId" },
                unique: true,
                filter: "[UsuarioId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OpinionRestaurante_UsuarioId_RestauranteId",
                table: "OpinionRestaurante");

            migrationBuilder.CreateIndex(
                name: "IX_OpinionRestaurante_UsuarioId",
                table: "OpinionRestaurante",
                column: "UsuarioId");
        }
    }
}
