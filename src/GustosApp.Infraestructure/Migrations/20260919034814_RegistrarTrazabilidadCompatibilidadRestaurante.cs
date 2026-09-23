using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GustosApp.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class RegistrarTrazabilidadCompatibilidadRestaurante : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EstadoDatosCompatibilidad",
                table: "Restaurantes",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Estimado");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaObtencionDatosCompatibilidadUtc",
                table: "Restaurantes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaUltimaVerificacionDatosCompatibilidadUtc",
                table: "Restaurantes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrigenDatosCompatibilidad",
                table: "Restaurantes",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Estimacion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstadoDatosCompatibilidad",
                table: "Restaurantes");

            migrationBuilder.DropColumn(
                name: "FechaObtencionDatosCompatibilidadUtc",
                table: "Restaurantes");

            migrationBuilder.DropColumn(
                name: "FechaUltimaVerificacionDatosCompatibilidadUtc",
                table: "Restaurantes");

            migrationBuilder.DropColumn(
                name: "OrigenDatosCompatibilidad",
                table: "Restaurantes");
        }
    }
}
