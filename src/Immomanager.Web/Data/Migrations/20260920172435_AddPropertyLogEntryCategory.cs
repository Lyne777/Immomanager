using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immomanager.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyLogEntryCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "PropertyLogEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFromArminAsset",
                table: "PropertyLogEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "PropertyLogEntries");

            migrationBuilder.DropColumn(
                name: "IsFromArminAsset",
                table: "PropertyLogEntries");
        }
    }
}
