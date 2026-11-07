using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immomanager.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMealCaloriesPortionsAndShoppingPersons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Persons",
                table: "ShoppingLists",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "CaloriesPerServing",
                table: "PlannedMeals",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PortionItems",
                table: "PlannedMeals",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Persons",
                table: "ShoppingLists");

            migrationBuilder.DropColumn(
                name: "CaloriesPerServing",
                table: "PlannedMeals");

            migrationBuilder.DropColumn(
                name: "PortionItems",
                table: "PlannedMeals");
        }
    }
}
