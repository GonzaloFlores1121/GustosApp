using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GustosApp.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class SepararVigenciaOfertaYDeclaraciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaRevisionOfertaUtc",
                table: "Restaurantes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaRevisionRestriccionesUtc",
                table: "Restaurantes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VersionOferta",
                table: "Restaurantes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VersionRestricciones",
                table: "Restaurantes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VigenciaGustosJson",
                table: "Restaurantes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VigenciaRestriccionesJson",
                table: "Restaurantes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaRevisionOfertaUtc",
                table: "Restaurantes");

            migrationBuilder.DropColumn(
                name: "FechaRevisionRestriccionesUtc",
                table: "Restaurantes");

            migrationBuilder.DropColumn(
                name: "VersionOferta",
                table: "Restaurantes");

            migrationBuilder.DropColumn(
                name: "VersionRestricciones",
                table: "Restaurantes");

            migrationBuilder.DropColumn(
                name: "VigenciaGustosJson",
                table: "Restaurantes");

            migrationBuilder.DropColumn(
                name: "VigenciaRestriccionesJson",
                table: "Restaurantes");
        }
    }
}
