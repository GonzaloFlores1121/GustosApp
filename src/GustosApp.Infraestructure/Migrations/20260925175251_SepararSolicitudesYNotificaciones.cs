using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GustosApp.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class SepararSolicitudesYNotificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvitacionesGrupos_GrupoId",
                table: "InvitacionesGrupos");

            migrationBuilder.AddColumn<bool>(
                name: "CorreoRechazoEnviado",
                table: "SolicitudesRestaurantes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ClaveUnica",
                table: "Notificaciones",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrlDestino",
                table: "Notificaciones",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "InvitacionesGrupos",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_ClaveUnica",
                table: "Notificaciones",
                column: "ClaveUnica",
                unique: true,
                filter: "[ClaveUnica] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionesGrupos_GrupoId_UsuarioInvitadoId",
                table: "InvitacionesGrupos",
                columns: new[] { "GrupoId", "UsuarioInvitadoId" },
                unique: true,
                filter: "[Estado] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notificaciones_ClaveUnica",
                table: "Notificaciones");

            migrationBuilder.DropIndex(
                name: "IX_InvitacionesGrupos_GrupoId_UsuarioInvitadoId",
                table: "InvitacionesGrupos");

            migrationBuilder.DropColumn(
                name: "CorreoRechazoEnviado",
                table: "SolicitudesRestaurantes");

            migrationBuilder.DropColumn(
                name: "ClaveUnica",
                table: "Notificaciones");

            migrationBuilder.DropColumn(
                name: "UrlDestino",
                table: "Notificaciones");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "InvitacionesGrupos");

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionesGrupos_GrupoId",
                table: "InvitacionesGrupos",
                column: "GrupoId");
        }
    }
}
