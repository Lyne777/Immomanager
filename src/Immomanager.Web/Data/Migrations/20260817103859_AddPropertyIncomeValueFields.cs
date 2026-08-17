using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immomanager.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyIncomeValueFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ColdRentMonthlyAtPurchase",
                table: "Properties",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IncomeMultiplier",
                table: "Properties",
                type: "TEXT",
                precision: 5,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColdRentMonthlyAtPurchase",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "IncomeMultiplier",
                table: "Properties");
        }
    }
}
