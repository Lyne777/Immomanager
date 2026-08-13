namespace Immomanager.Web.Services;

/// <summary>Persistente Backup-Einstellungen. Wird bewusst als eigene JSON-Datei im Datenverzeichnis
/// gespeichert statt in appsettings.json (das Deployment-Konfiguration ist, keine zur Laufzeit vom
/// Nutzer änderbaren Einstellungen) oder in der SQLite-Datenbank (die selbst Sicherungsgegenstand
/// ist - eine Einstellung "wie oft sichern" darf nicht durch ein Restore auf einen alten DB-Stand
/// mit zurückgesetzt werden).</summary>
public class BackupSettings
{
    public bool AutoBackupOnStartupEnabled { get; set; }

    public int AutoBackupRetentionCount { get; set; } = 5;
}
