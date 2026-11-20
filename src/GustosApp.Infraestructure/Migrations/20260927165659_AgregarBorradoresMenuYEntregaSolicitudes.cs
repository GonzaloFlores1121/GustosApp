using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GustosApp.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarBorradoresMenuYEntregaSolicitudes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ArchivosRechazoEliminados",
                table: "SolicitudesRestaurantes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CorreoNuevaSolicitudEnviado",
                table: "SolicitudesRestaurantes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BorradorError",
                table: "RestauranteMenus",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BorradorImagenFuenteUrl",
                table: "RestauranteMenus",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BorradorJson",
                table: "RestauranteMenus",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BorradorToken",
                table: "RestauranteMenus",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstadoBorrador",
                table: "RestauranteMenus",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Ninguno");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaBorradorUtc",
                table: "RestauranteMenus",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagenFuentePublicadaUrl",
                table: "RestauranteMenus",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivosRechazoEliminados",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropColumn(
                name: "CorreoNuevaSolicitudEnviado",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropColumn(
                name: "BorradorError",
                table: "RestauranteMenus");

            migrationBuilder.DropColumn(
                name: "BorradorImagenFuenteUrl",
                table: "RestauranteMenus");

            migrationBuilder.DropColumn(
                name: "BorradorJson",
                table: "RestauranteMenus");

            migrationBuilder.DropColumn(
                name: "BorradorToken",
                table: "RestauranteMenus");

            migrationBuilder.DropColumn(
                name: "EstadoBorrador",
                table: "RestauranteMenus");

            migrationBuilder.DropColumn(
                name: "FechaBorradorUtc",
                table: "RestauranteMenus");

            migrationBuilder.DropColumn(
                name: "ImagenFuentePublicadaUrl",
                table: "RestauranteMenus");
        }
    }
}
