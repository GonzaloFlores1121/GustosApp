using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GustosApp.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class ChatConfiable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_GrupoId",
                table: "ChatMessages");

            migrationBuilder.AlterColumn<string>(
                name: "Mensaje",
                table: "ChatMessages",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "OperacionId",
                table: "ChatMessages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_GrupoId_FechaEnvio_Id",
                table: "ChatMessages",
                columns: new[] { "GrupoId", "FechaEnvio", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_GrupoId_UsuarioId_OperacionId",
                table: "ChatMessages",
                columns: new[] { "GrupoId", "UsuarioId", "OperacionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_GrupoId_FechaEnvio_Id",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_GrupoId_UsuarioId_OperacionId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "OperacionId",
                table: "ChatMessages");

            migrationBuilder.AlterColumn<string>(
                name: "Mensaje",
                table: "ChatMessages",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_GrupoId",
                table: "ChatMessages",
                column: "GrupoId");
        }
    }
}
