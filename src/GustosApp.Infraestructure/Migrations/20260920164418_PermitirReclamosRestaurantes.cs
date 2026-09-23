using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GustosApp.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class PermitirReclamosRestaurantes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CorreoAprobacionEnviado",
                table: "SolicitudesRestaurantes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "RestauranteAprobadoId",
                table: "SolicitudesRestaurantes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RestauranteExistenteId",
                table: "SolicitudesRestaurantes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RolFirebaseSincronizado",
                table: "SolicitudesRestaurantes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesRestaurantes_RestauranteAprobadoId",
                table: "SolicitudesRestaurantes",
                column: "RestauranteAprobadoId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesRestaurantes_RestauranteExistenteId",
                table: "SolicitudesRestaurantes",
                column: "RestauranteExistenteId");

            migrationBuilder.AddForeignKey(
                name: "FK_SolicitudesRestaurantes_Restaurantes_RestauranteAprobadoId",
                table: "SolicitudesRestaurantes",
                column: "RestauranteAprobadoId",
                principalTable: "Restaurantes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SolicitudesRestaurantes_Restaurantes_RestauranteExistenteId",
                table: "SolicitudesRestaurantes",
                column: "RestauranteExistenteId",
                principalTable: "Restaurantes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SolicitudesRestaurantes_Restaurantes_RestauranteAprobadoId",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropForeignKey(
                name: "FK_SolicitudesRestaurantes_Restaurantes_RestauranteExistenteId",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropIndex(
                name: "IX_SolicitudesRestaurantes_RestauranteAprobadoId",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropIndex(
                name: "IX_SolicitudesRestaurantes_RestauranteExistenteId",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropColumn(
                name: "CorreoAprobacionEnviado",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropColumn(
                name: "RestauranteAprobadoId",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropColumn(
                name: "RestauranteExistenteId",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropColumn(
                name: "RolFirebaseSincronizado",
                table: "SolicitudesRestaurantes");
        }
    }
}
