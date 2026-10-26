namespace Immomanager.Web.Services;

/// <summary>Aufgelöste, absolute Dateisystempfade für das konfigurierte Datenverzeichnis
/// (SQLite-Datei und hochgeladene Bilder). Wird einmal in Program.cs berechnet und als
/// Singleton registriert, damit alle Services denselben Ort verwenden.</summary>
public class StorageOptions
{
    public const string UploadsRelativeRoot = "uploads";
    public const string ExportsRelativeRoot = "exports";
    public const string BackupsRelativeRoot = "backups";
    public const string PoliciesRelativeRoot = "policies";
    public const string UtilityStatementsRelativeRoot = "utility_statements";
    public const string LeasesRelativeRoot = "leases";
    public const string DocumentsRelativeRoot = "documents";
    public const string LetterheadsRelativeRoot = "letterheads";
    public const string ClaudeExportRelativeRoot = "claude-export";

    public required string DataDirectoryAbsolute { get; init; }

    public required string UploadsDirectoryAbsolute { get; init; }

    public required string ExportsDirectoryAbsolute { get; init; }

    public required string BackupsDirectoryAbsolute { get; init; }

    public required string PoliciesDirectoryAbsolute { get; init; }

    public required string UtilityStatementsDirectoryAbsolute { get; init; }

    public required string LeasesDirectoryAbsolute { get; init; }

    public required string DocumentsDirectoryAbsolute { get; init; }

    public required string LetterheadsDirectoryAbsolute { get; init; }

    /// <summary>Zielordner des täglichen Exports für Claude (siehe <see cref="ClaudeExportService"/>) -
    /// standardmäßig unterhalb des Datenverzeichnisses, per "Storage:ClaudeExportDirectory" auf einen
    /// beliebigen (z. B. per Cloud Sync mit OneDrive abgeglichenen) Ordner umlenkbar.</summary>
    public required string ClaudeExportDirectoryAbsolute { get; init; }

    /// <summary>Absoluter Pfad zur aktiven SQLite-Datenbankdatei (Ziel von Backup/Restore).</summary>
    public required string DatabaseFilePath { get; init; }
}
