using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GustosApp.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class CorregirEtiquetasVegetarianas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CondicionMedicaTags",
                columns: new[] { "CondicionesMedicasId", "TagsId" },
                values: new object[,]
                {
                    { new Guid("44444444-0001-0001-0001-000000000013"), new Guid("11111111-1111-1111-1111-111111111115") },
                    { new Guid("44444444-0001-0001-0001-000000000014"), new Guid("11111111-1111-1111-1111-111111111115") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CondicionMedicaTags",
                keyColumns: new[] { "CondicionesMedicasId", "TagsId" },
                keyValues: new object[] { new Guid("44444444-0001-0001-0001-000000000013"), new Guid("11111111-1111-1111-1111-111111111115") });

            migrationBuilder.DeleteData(
                table: "CondicionMedicaTags",
                keyColumns: new[] { "CondicionesMedicasId", "TagsId" },
                keyValues: new object[] { new Guid("44444444-0001-0001-0001-000000000014"), new Guid("11111111-1111-1111-1111-111111111115") });
        }
    }
}
