using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immomanager.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Properties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    YearBuilt = table.Column<int>(type: "INTEGER", nullable: true),
                    LivingAreaSqm = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    PurchaseDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PropertyTransferTax = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    NotaryAndRegistryCosts = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    BrokerCommission = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    InitialRenovationCosts = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    OwnershipSharePercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    CurrentMarketValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CurrentColdRentMonthly = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    NonAllocableCostsMonthly = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Properties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Financings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PropertyId = table.Column<int>(type: "INTEGER", nullable: false),
                    BankName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OriginalLoanAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CurrentRemainingDebt = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    InterestRatePercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 3, nullable: false),
                    InitialRepaymentRatePercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 3, nullable: false),
                    MonthlyPayment = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    FixedInterestEndDate = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Financings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Financings_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Financings_PropertyId",
                table: "Financings",
                column: "PropertyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Financings");

            migrationBuilder.DropTable(
                name: "Properties");
        }
    }
}
