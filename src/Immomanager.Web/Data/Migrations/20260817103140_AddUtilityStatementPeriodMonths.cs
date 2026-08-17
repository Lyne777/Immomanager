using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immomanager.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUtilityStatementPeriodMonths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default 12 (nicht 0) fuer bestehende Abrechnungen - das entspricht der bisherigen
            // impliziten Annahme (immer volles Kalenderjahr) und aendert damit ihr Verhalten nicht.
            migrationBuilder.AddColumn<decimal>(
                name: "PeriodMonths",
                table: "UtilityStatements",
                type: "TEXT",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 12m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PeriodMonths",
                table: "UtilityStatements");
        }
    }
}
