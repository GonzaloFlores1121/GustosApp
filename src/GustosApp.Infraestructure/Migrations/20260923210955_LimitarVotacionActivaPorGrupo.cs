using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GustosApp.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class LimitarVotacionActivaPorGrupo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH VotacionesActivasDuplicadas AS
                (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY GrupoId
                               ORDER BY FechaInicio DESC, Id DESC) AS Numero
                    FROM Votaciones
                    WHERE Estado = N'Activa'
                )
                UPDATE v
                SET Estado = N'Cancelada',
                    FechaCierre = COALESCE(v.FechaCierre, SYSUTCDATETIME()),
                    RestauranteGanadorId = NULL
                FROM Votaciones v
                INNER JOIN VotacionesActivasDuplicadas d ON d.Id = v.Id
                WHERE d.Numero > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Votaciones_GrupoId",
                table: "Votaciones",
                column: "GrupoId",
                unique: true,
                filter: "[Estado] = N'Activa'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Votaciones_GrupoId",
                table: "Votaciones");
        }
    }
}
