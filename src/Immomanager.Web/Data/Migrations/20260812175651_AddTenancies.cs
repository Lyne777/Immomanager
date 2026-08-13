using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immomanager.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenancies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tenancies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PropertyUnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    TenantName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TenantEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TenantPhone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    MoveInDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    MoveOutDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ColdRentMonthly = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AdvancePaymentMonthly = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SecurityDeposit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    PdfFilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PdfFileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenancies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tenancies_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenancies_PropertyUnitId",
                table: "Tenancies",
                column: "PropertyUnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tenancies");
        }
    }
}
