using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GustosApp.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class RegistrarActualizacionExternaRestaurantes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorUltimaActualizacionExterna",
                table: "Restaurantes",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstadoUltimaActualizacionExterna",
                table: "Restaurantes",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Nunca");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaUltimaActualizacionExternaExitosaUtc",
                table: "Restaurantes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaUltimoIntentoActualizacionExternaUtc",
                table: "Restaurantes",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErrorUltimaActualizacionExterna",
                table: "Restaurantes");

            migrationBuilder.DropColumn(
                name: "EstadoUltimaActualizacionExterna",
                table: "Restaurantes");

            migrationBuilder.DropColumn(
                name: "FechaUltimaActualizacionExternaExitosaUtc",
                table: "Restaurantes");

            migrationBuilder.DropColumn(
                name: "FechaUltimoIntentoActualizacionExternaUtc",
                table: "Restaurantes");
        }
    }
}
