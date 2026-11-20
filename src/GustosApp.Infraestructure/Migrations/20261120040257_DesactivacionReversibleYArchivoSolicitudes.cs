using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GustosApp.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class DesactivacionReversibleYArchivoSolicitudes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Archivada",
                table: "SolicitudesRestaurantes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCambioArchivoUtc",
                table: "SolicitudesRestaurantes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VersionArchivo",
                table: "SolicitudesRestaurantes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Desactivado",
                table: "Restaurantes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCambioDisponibilidadUtc",
                table: "Restaurantes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoDisponibilidad",
                table: "Restaurantes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VersionDisponibilidad",
                table: "Restaurantes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "RegistrosAdministracionRestaurantes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestauranteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUid = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosAdministracionRestaurantes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrosAdministracionRestaurantes_Restaurantes_RestauranteId",
                        column: x => x.RestauranteId,
                        principalTable: "Restaurantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistrosAdministracionRestaurantes_SolicitudesRestaurantes_SolicitudId",
                        column: x => x.SolicitudId,
                        principalTable: "SolicitudesRestaurantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAdministracionRestaurantes_RestauranteId_FechaUtc",
                table: "RegistrosAdministracionRestaurantes",
                columns: new[] { "RestauranteId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAdministracionRestaurantes_SolicitudId_FechaUtc",
                table: "RegistrosAdministracionRestaurantes",
                columns: new[] { "SolicitudId", "FechaUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrosAdministracionRestaurantes");

            migrationBuilder.DropColumn(
                name: "Archivada",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropColumn(
                name: "FechaCambioArchivoUtc",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropColumn(
                name: "VersionArchivo",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropColumn(
                name: "Desactivado",
                table: "Restaurantes");

            migrationBuilder.DropColumn(
                name: "FechaCambioDisponibilidadUtc",
                table: "Restaurantes");

            migrationBuilder.DropColumn(
                name: "MotivoDisponibilidad",
                table: "Restaurantes");

            migrationBuilder.DropColumn(
                name: "VersionDisponibilidad",
                table: "Restaurantes");
        }
    }
}
