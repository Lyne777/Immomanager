using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immomanager.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDealCalculations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DealCalculations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    VersionGroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    PropertyId = table.Column<int>(type: "INTEGER", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    LivingAreaSqm = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    YearBuilt = table.Column<int>(type: "INTEGER", nullable: true),
                    PurchaseDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ParkingSpaces = table.Column<int>(type: "INTEGER", nullable: false),
                    BrokerFeePercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    NotaryFeePercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    LandRegistryFeePercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    RealEstateTransferTaxPercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    OtherAcquisitionCosts = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    InitialRenovationCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    RentalIncomeMode = table.Column<int>(type: "INTEGER", nullable: false),
                    GlobalMonthlyNetColdRent = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ParkingIncomeMonthly = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    OtherIncomeMonthly = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    RentIncreasePercentPa = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    NonAllocableCostsMonthly = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    MaintenanceReservePerSqmPa = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    VacancyRiskPercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    CostInflationPercentPa = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    BuildingSharePercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    AfaRatePercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    UseMonumentAfa = table.Column<bool>(type: "INTEGER", nullable: false),
                    PersonalMarginalTaxRatePercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    AnnualValueAppreciationPercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    ProjectionYears = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealCalculations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealCalculations_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CalculationScenarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DealCalculationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PurchasePriceDeltaPercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    InterestRateDeltaPercentPoints = table.Column<decimal>(type: "TEXT", precision: 5, scale: 3, nullable: false),
                    RentDeltaPercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalculationScenarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalculationScenarios_DealCalculations_DealCalculationId",
                        column: x => x.DealCalculationId,
                        principalTable: "DealCalculations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoanCalculations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DealCalculationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LoanAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    InterestRatePercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 3, nullable: false),
                    InitialRepaymentRatePercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 3, nullable: false),
                    AnnualSpecialRepayment = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    FixedInterestYears = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanCalculations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanCalculations_DealCalculations_DealCalculationId",
                        column: x => x.DealCalculationId,
                        principalTable: "DealCalculations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnitCalculations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DealCalculationId = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitLabel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AreaSqm = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    CurrentRentMonthly = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TargetRentMonthly = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TargetRentReachedInYear = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitCalculations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitCalculations_DealCalculations_DealCalculationId",
                        column: x => x.DealCalculationId,
                        principalTable: "DealCalculations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalculationScenarios_DealCalculationId",
                table: "CalculationScenarios",
                column: "DealCalculationId");

            migrationBuilder.CreateIndex(
                name: "IX_DealCalculations_PropertyId",
                table: "DealCalculations",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanCalculations_DealCalculationId",
                table: "LoanCalculations",
                column: "DealCalculationId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitCalculations_DealCalculationId",
                table: "UnitCalculations",
                column: "DealCalculationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalculationScenarios");

            migrationBuilder.DropTable(
                name: "LoanCalculations");

            migrationBuilder.DropTable(
                name: "UnitCalculations");

            migrationBuilder.DropTable(
                name: "DealCalculations");
        }
    }
}
