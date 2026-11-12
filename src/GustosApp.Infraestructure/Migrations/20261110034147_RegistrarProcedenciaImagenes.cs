using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GustosApp.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class RegistrarProcedenciaImagenes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrigenImagen",
                table: "Restaurantes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrigenLogo",
                table: "Restaurantes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrigenImagenBorrador",
                table: "RestauranteMenus",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrigenImagenPublicada",
                table: "RestauranteMenus",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Origen",
                table: "RestauranteImagenes",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrigenImagen",
                table: "Restaurantes");

            migrationBuilder.DropColumn(
                name: "OrigenLogo",
                table: "Restaurantes");

            migrationBuilder.DropColumn(
                name: "OrigenImagenBorrador",
                table: "RestauranteMenus");

            migrationBuilder.DropColumn(
                name: "OrigenImagenPublicada",
                table: "RestauranteMenus");

            migrationBuilder.DropColumn(
                name: "Origen",
                table: "RestauranteImagenes");
        }
    }
}
