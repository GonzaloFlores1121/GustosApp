using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GustosApp.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class MantenerUsuarioDuranteSolicitudRestaurante : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Usuarios SET Rol = 0 WHERE Rol = 1");

            migrationBuilder.DropIndex(
                name: "IX_SolicitudesRestaurantes_UsuarioId",
                table: "SolicitudesRestaurantes");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesRestaurantes_UsuarioId",
                table: "SolicitudesRestaurantes",
                column: "UsuarioId",
                unique: true,
                filter: "[Estado] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SolicitudesRestaurantes_UsuarioId",
                table: "SolicitudesRestaurantes");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesRestaurantes_UsuarioId",
                table: "SolicitudesRestaurantes",
                column: "UsuarioId");
        }
    }
}
