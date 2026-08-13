using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immomanager.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyUnitsAndInsurance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InsuranceCheckItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PropertyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GroupLabel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCovered = table.Column<bool>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InsuranceCheckItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InsuranceCheckItems_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InsurancePolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PropertyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PolicyNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AnnualPremium = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ExpirationDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    PdfFilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PdfFileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InsurancePolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InsurancePolicies_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertyUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PropertyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AreaSqm = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    ColdRentMonthly = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    NonAllocableCostsMonthly = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyUnits_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Bestehende Objekt-Summenwerte in eine erste Einheit je Immobilie überführen, BEVOR die
            // alten Property-Spalten gelöscht werden, damit keine Daten verloren gehen.
            migrationBuilder.Sql("""
                INSERT INTO "PropertyUnits" ("PropertyId", "Label", "AreaSqm", "ColdRentMonthly", "NonAllocableCostsMonthly")
                SELECT "Id", 'Einheit 1', "LivingAreaSqm", "CurrentColdRentMonthly", "NonAllocableCostsMonthly"
                FROM "Properties";
                """);

            migrationBuilder.DropColumn(
                name: "CurrentColdRentMonthly",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "LivingAreaSqm",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "NonAllocableCostsMonthly",
                table: "Properties");

            migrationBuilder.CreateIndex(
                name: "IX_InsuranceCheckItems_PropertyId_Key",
                table: "InsuranceCheckItems",
                columns: new[] { "PropertyId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InsurancePolicies_PropertyId_Category",
                table: "InsurancePolicies",
                columns: new[] { "PropertyId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyUnits_PropertyId",
                table: "PropertyUnits",
                column: "PropertyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CurrentColdRentMonthly",
                table: "Properties",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LivingAreaSqm",
                table: "Properties",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NonAllocableCostsMonthly",
                table: "Properties",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Summenwerte aus den Einheiten zurück in die Property-Spalten schreiben, bevor die
            // Einheiten-Tabelle gelöscht wird (bestmögliche Wiederherstellung, kollabiert mehrere
            // Einheiten auf die vorherigen Summenfelder).
            migrationBuilder.Sql("""
                UPDATE "Properties"
                SET "LivingAreaSqm" = COALESCE((SELECT SUM("AreaSqm") FROM "PropertyUnits" WHERE "PropertyUnits"."PropertyId" = "Properties"."Id"), 0),
                    "CurrentColdRentMonthly" = COALESCE((SELECT SUM("ColdRentMonthly") FROM "PropertyUnits" WHERE "PropertyUnits"."PropertyId" = "Properties"."Id"), 0),
                    "NonAllocableCostsMonthly" = COALESCE((SELECT SUM("NonAllocableCostsMonthly") FROM "PropertyUnits" WHERE "PropertyUnits"."PropertyId" = "Properties"."Id"), 0);
                """);

            migrationBuilder.DropTable(
                name: "InsuranceCheckItems");

            migrationBuilder.DropTable(
                name: "InsurancePolicies");

            migrationBuilder.DropTable(
                name: "PropertyUnits");
        }
    }
}
