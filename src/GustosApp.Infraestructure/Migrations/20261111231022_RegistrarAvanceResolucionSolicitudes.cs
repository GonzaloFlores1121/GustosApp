using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GustosApp.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class RegistrarAvanceResolucionSolicitudes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CorreoResolucion",
                table: "SolicitudesRestaurantes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MenuResolucion",
                table: "SolicitudesRestaurantes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "NotificacionResolucionPublicada",
                table: "SolicitudesRestaurantes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TokenMenuResolucion",
                table: "SolicitudesRestaurantes",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorreoResolucion",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropColumn(
                name: "MenuResolucion",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropColumn(
                name: "NotificacionResolucionPublicada",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropColumn(
                name: "TokenMenuResolucion",
                table: "SolicitudesRestaurantes");
        }
    }
}
