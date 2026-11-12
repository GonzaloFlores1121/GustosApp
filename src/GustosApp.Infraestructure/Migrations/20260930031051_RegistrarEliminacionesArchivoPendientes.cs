using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GustosApp.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class RegistrarEliminacionesArchivoPendientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EliminacionesArchivoPendientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CarpetaEsperada = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProximoIntentoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    UltimoError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EliminacionesArchivoPendientes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EliminacionesArchivoPendientes_ProximoIntentoUtc",
                table: "EliminacionesArchivoPendientes",
                column: "ProximoIntentoUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EliminacionesArchivoPendientes_Url_CarpetaEsperada",
                table: "EliminacionesArchivoPendientes",
                columns: new[] { "Url", "CarpetaEsperada" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EliminacionesArchivoPendientes");
        }
    }
}
