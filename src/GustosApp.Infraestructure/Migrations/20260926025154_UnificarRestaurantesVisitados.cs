using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GustosApp.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class UnificarRestaurantesVisitados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [UsuarioRestauranteVisitados]
                    WHERE [RestauranteId] IS NOT NULL
                    GROUP BY [UsuarioId], [RestauranteId]
                    HAVING COUNT(*) > 1
                ) OR EXISTS (
                    SELECT 1
                    FROM [UsuarioRestauranteVisitados]
                    WHERE [PlaceId] IS NOT NULL
                    GROUP BY [UsuarioId], [PlaceId]
                    HAVING COUNT(*) > 1
                )
                    THROW 51000, 'Hay restaurantes visitados duplicados. Revisalos antes de aplicar esta migracion.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_UsuarioRestauranteVisitados_UsuarioId",
                table: "UsuarioRestauranteVisitados");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioRestauranteVisitados_UsuarioId_PlaceId",
                table: "UsuarioRestauranteVisitados",
                columns: new[] { "UsuarioId", "PlaceId" },
                unique: true,
                filter: "[PlaceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioRestauranteVisitados_UsuarioId_RestauranteId",
                table: "UsuarioRestauranteVisitados",
                columns: new[] { "UsuarioId", "RestauranteId" },
                unique: true,
                filter: "[RestauranteId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UsuarioRestauranteVisitados_UsuarioId_PlaceId",
                table: "UsuarioRestauranteVisitados");

            migrationBuilder.DropIndex(
                name: "IX_UsuarioRestauranteVisitados_UsuarioId_RestauranteId",
                table: "UsuarioRestauranteVisitados");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioRestauranteVisitados_UsuarioId",
                table: "UsuarioRestauranteVisitados",
                column: "UsuarioId");
        }
    }
}
