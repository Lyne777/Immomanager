using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immomanager.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUtilityStatementPropertyUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UtilityStatements_PropertyId_Year",
                table: "UtilityStatements");

            migrationBuilder.AddColumn<int>(
                name: "PropertyUnitId",
                table: "UtilityStatements",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UtilityStatements_PropertyId_Year",
                table: "UtilityStatements",
                columns: new[] { "PropertyId", "Year" },
                unique: true,
                filter: "\"PropertyUnitId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityStatements_PropertyUnitId_Year",
                table: "UtilityStatements",
                columns: new[] { "PropertyUnitId", "Year" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UtilityStatements_PropertyUnits_PropertyUnitId",
                table: "UtilityStatements",
                column: "PropertyUnitId",
                principalTable: "PropertyUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UtilityStatements_PropertyUnits_PropertyUnitId",
                table: "UtilityStatements");

            migrationBuilder.DropIndex(
                name: "IX_UtilityStatements_PropertyId_Year",
                table: "UtilityStatements");

            migrationBuilder.DropIndex(
                name: "IX_UtilityStatements_PropertyUnitId_Year",
                table: "UtilityStatements");

            migrationBuilder.DropColumn(
                name: "PropertyUnitId",
                table: "UtilityStatements");

            migrationBuilder.CreateIndex(
                name: "IX_UtilityStatements_PropertyId_Year",
                table: "UtilityStatements",
                columns: new[] { "PropertyId", "Year" },
                unique: true);
        }
    }
}
