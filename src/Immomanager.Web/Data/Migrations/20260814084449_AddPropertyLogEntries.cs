using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immomanager.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyLogEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PropertyLogEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PropertyId = table.Column<int>(type: "INTEGER", nullable: false),
                    PropertyUnitId = table.Column<int>(type: "INTEGER", nullable: true),
                    DateLabel = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyLogEntries_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PropertyLogEntries_PropertyUnits_PropertyUnitId",
                        column: x => x.PropertyUnitId,
                        principalTable: "PropertyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyLogEntries_PropertyId",
                table: "PropertyLogEntries",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyLogEntries_PropertyUnitId",
                table: "PropertyLogEntries",
                column: "PropertyUnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropertyLogEntries");
        }
    }
}
