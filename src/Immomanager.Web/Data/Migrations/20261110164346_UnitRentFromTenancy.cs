using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Immomanager.Web.Data.Migrations
{
    /// <summary>Die Kaltmiete einer Einheit wird nicht mehr getrennt gepflegt, sondern ergibt sich aus
    /// dem laufenden Mietverhältnis (einzige Quelle: Tenancies.ColdRentMonthly). Die Spalte
    /// PropertyUnits.ColdRentMonthly entfällt.
    ///
    /// Damit dabei keine Miete verloren geht oder Kennzahlen unbemerkt springen:
    /// 1. Die bisherigen Einheiten-Mieten werden vorab in die Tabelle "LegacyUnitRents" gesichert
    ///    (die App migriert beim Start und sichert erst danach; Down stellt die Spalte daraus wieder her).
    /// 2. Hat das laufende Mietverhältnis einer Einheit keine Miete (0), die Einheit aber schon, wird die
    ///    Einheiten-Miete ins Mietverhältnis übernommen. Hat das Mietverhältnis eine Miete, gilt sie
    ///    (der Vertrag ist maßgeblich) - auch wenn sie von der alten Einheiten-Miete abweicht.
    /// Einheiten ohne laufendes Mietverhältnis zählen künftig mit 0 (Leerstand).</summary>
    public partial class UnitRentFromTenancy : Migration
    {
        // Platzhalter-Mieter aus einem Mietvertrags-Upload (Tenancy.PendingTenantName) - bewusst als
        // Literal, damit die Migration unabhängig vom späteren Stand des Modells bleibt.
        private const string PendingTenantName = "(noch nicht ausgefüllt - Mietvertrag wartet auf Auswertung)";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "LegacyUnitRents" (
                    "PropertyUnitId" INTEGER NOT NULL,
                    "ColdRentMonthly" TEXT NOT NULL,
                    "SavedAt" TEXT NOT NULL
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO "LegacyUnitRents" ("PropertyUnitId", "ColdRentMonthly", "SavedAt")
                SELECT "Id", "ColdRentMonthly", datetime('now') FROM "PropertyUnits";
                """);

            // Dezimalwerte stehen als TEXT in der Datenbank - Vergleiche daher immer über CAST, sonst würde
            // z. B. '0.00' nicht als 0 erkannt.
            migrationBuilder.Sql($"""
                UPDATE "Tenancies"
                SET "ColdRentMonthly" = (SELECT u."ColdRentMonthly" FROM "PropertyUnits" u WHERE u."Id" = "Tenancies"."PropertyUnitId")
                WHERE CAST("ColdRentMonthly" AS REAL) = 0
                  AND CAST((SELECT u."ColdRentMonthly" FROM "PropertyUnits" u WHERE u."Id" = "Tenancies"."PropertyUnitId") AS REAL) > 0
                  AND "Id" = (
                      SELECT t."Id" FROM "Tenancies" t
                      WHERE t."PropertyUnitId" = "Tenancies"."PropertyUnitId"
                        AND t."TenantName" <> '{PendingTenantName}'
                        AND t."MoveInDate" <= date('now', 'localtime')
                        AND (t."MoveOutDate" IS NULL OR t."MoveOutDate" >= date('now', 'localtime'))
                      ORDER BY t."MoveInDate" DESC, t."Id" DESC
                      LIMIT 1);
                """);

            migrationBuilder.DropColumn(
                name: "ColdRentMonthly",
                table: "PropertyUnits");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ColdRentMonthly",
                table: "PropertyUnits",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Stellt die gesicherten Einheiten-Mieten wieder her. Die in Up ins Mietverhältnis
            // übernommenen Mieten bleiben bewusst bestehen (vorher 0, daher unkritisch).
            migrationBuilder.Sql("""
                UPDATE "PropertyUnits"
                SET "ColdRentMonthly" = COALESCE(
                    (SELECT l."ColdRentMonthly" FROM "LegacyUnitRents" l WHERE l."PropertyUnitId" = "PropertyUnits"."Id" ORDER BY l."SavedAt" DESC LIMIT 1),
                    '0');
                """);
        }
    }
}
