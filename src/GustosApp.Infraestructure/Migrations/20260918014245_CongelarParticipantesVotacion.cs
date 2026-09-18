using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GustosApp.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class CongelarParticipantesVotacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VotacionParticipantes",
                columns: table => new
                {
                    VotacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VotacionParticipantes", x => new { x.VotacionId, x.UsuarioId });
                    table.ForeignKey(
                        name: "FK_VotacionParticipantes_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VotacionParticipantes_Votaciones_VotacionId",
                        column: x => x.VotacionId,
                        principalTable: "Votaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VotacionParticipantes_UsuarioId",
                table: "VotacionParticipantes",
                column: "UsuarioId");

            migrationBuilder.Sql(
                """
                INSERT INTO VotacionParticipantes (VotacionId, UsuarioId)
                SELECT DISTINCT VotacionId, UsuarioId
                FROM Votos;

                INSERT INTO VotacionParticipantes (VotacionId, UsuarioId)
                SELECT v.Id, m.UsuarioId
                FROM Votaciones v
                INNER JOIN MiembrosGrupos m ON m.GrupoId = v.GrupoId
                WHERE v.Estado = 'Activa'
                  AND m.Activo = 1
                  AND m.afectarRecomendacion = 1
                  AND NOT EXISTS (
                      SELECT 1
                      FROM VotacionParticipantes vp
                      WHERE vp.VotacionId = v.Id
                        AND vp.UsuarioId = m.UsuarioId
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VotacionParticipantes");
        }
    }
}
