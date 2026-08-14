using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immomanager.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRepaymentVehicles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RepaymentVehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FinancingId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TargetAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    MonthlyContribution = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CurrentValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ContractEndDate = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepaymentVehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepaymentVehicles_Financings_FinancingId",
                        column: x => x.FinancingId,
                        principalTable: "Financings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepaymentVehicles_FinancingId",
                table: "RepaymentVehicles",
                column: "FinancingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepaymentVehicles");
        }
    }
}
