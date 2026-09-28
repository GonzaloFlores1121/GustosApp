using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GustosApp.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class ComprasPremiumAuditables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AccesoPlusLegado",
                table: "Usuarios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Los Plus anteriores se conservan como acceso heredado; no se inventa un pago aprobado.
            migrationBuilder.Sql("UPDATE [Usuarios] SET [AccesoPlusLegado] = 1 WHERE [Plan] = 1");

            migrationBuilder.CreateTable(
                name: "ComprasPremium",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Producto = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ImporteEsperado = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MonedaEsperada = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    EntornoEsperado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    ClaveIdempotencia = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrdenProveedorId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UrlCheckout = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiraUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimoErrorCodigo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PosibleCobroDuplicado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComprasPremium", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComprasPremium_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PagosPremiumProveedor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompraPremiumId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PagoProveedorId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Importe = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ImporteReembolsado = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Moneda = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    EntornoProduccion = table.Column<bool>(type: "bit", nullable: false),
                    CuentaReceptoraId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FechaProveedorUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimaConsultaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstadoProveedor = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagosPremiumProveedor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PagosPremiumProveedor_ComprasPremium_CompraPremiumId",
                        column: x => x.CompraPremiumId,
                        principalTable: "ComprasPremium",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComprasPremium_ClaveIdempotencia",
                table: "ComprasPremium",
                column: "ClaveIdempotencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComprasPremium_OrdenProveedorId",
                table: "ComprasPremium",
                column: "OrdenProveedorId",
                unique: true,
                filter: "[OrdenProveedorId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ComprasPremium_UsuarioId_Producto",
                table: "ComprasPremium",
                columns: new[] { "UsuarioId", "Producto" },
                unique: true,
                filter: "[Estado] IN (0, 1, 2, 8)");

            migrationBuilder.CreateIndex(
                name: "IX_PagosPremiumProveedor_CompraPremiumId",
                table: "PagosPremiumProveedor",
                column: "CompraPremiumId");

            migrationBuilder.CreateIndex(
                name: "IX_PagosPremiumProveedor_PagoProveedorId",
                table: "PagosPremiumProveedor",
                column: "PagoProveedorId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PagosPremiumProveedor");

            migrationBuilder.DropTable(
                name: "ComprasPremium");

            migrationBuilder.DropColumn(
                name: "AccesoPlusLegado",
                table: "Usuarios");
        }
    }
}
