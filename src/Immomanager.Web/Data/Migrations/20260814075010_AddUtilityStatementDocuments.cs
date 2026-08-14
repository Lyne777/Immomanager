using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immomanager.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUtilityStatementDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UtilityStatementDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UtilityStatementId = table.Column<int>(type: "INTEGER", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilityStatementDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UtilityStatementDocuments_UtilityStatements_UtilityStatementId",
                        column: x => x.UtilityStatementId,
                        principalTable: "UtilityStatements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UtilityStatementDocuments_UtilityStatementId",
                table: "UtilityStatementDocuments",
                column: "UtilityStatementId");

            // Bestehende Einzel-PDF-Zuordnungen (altes Schema: eine PDF je Abrechnung) in die neue
            // Dokumente-Tabelle übernehmen, bevor die alten Spalten entfernt werden.
            migrationBuilder.Sql(@"
                INSERT INTO UtilityStatementDocuments (UtilityStatementId, FilePath, FileName, UploadedAtUtc)
                SELECT Id, PdfFilePath, COALESCE(PdfFileName, PdfFilePath), datetime('now')
                FROM UtilityStatements
                WHERE PdfFilePath IS NOT NULL AND PdfFilePath <> '';
            ");

            migrationBuilder.DropColumn(
                name: "PdfFileName",
                table: "UtilityStatements");

            migrationBuilder.DropColumn(
                name: "PdfFilePath",
                table: "UtilityStatements");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UtilityStatementDocuments");

            migrationBuilder.AddColumn<string>(
                name: "PdfFileName",
                table: "UtilityStatements",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfFilePath",
                table: "UtilityStatements",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }
    }
}
