using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GustosApp.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarComprobantePrivadoReclamo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ComprobanteReclamo",
                table: "SolicitudesRestaurantes",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DeclaraAutorizacion",
                table: "SolicitudesRestaurantes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NombreSolicitante",
                table: "SolicitudesRestaurantes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelacionRestaurante",
                table: "SolicitudesRestaurantes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelefonoContacto",
                table: "SolicitudesRestaurantes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoComprobante",
                table: "SolicitudesRestaurantes",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComprobanteReclamo",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropColumn(
                name: "DeclaraAutorizacion",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropColumn(
                name: "NombreSolicitante",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropColumn(
                name: "RelacionRestaurante",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropColumn(
                name: "TelefonoContacto",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropColumn(
                name: "TipoComprobante",
                table: "SolicitudesRestaurantes");
        }
    }
}
