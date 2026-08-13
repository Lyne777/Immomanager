namespace Immomanager.Web.Models;

/// <summary>Metadaten zu einer einzelnen Datenbank-Sicherungsdatei im Backup-Verzeichnis. Ob eine
/// Sicherung automatisch oder manuell erstellt wurde, ergibt sich rein aus dem Dateinamens-Präfix
/// ("auto_"/"manual_") - es gibt bewusst keine separate Metadaten-Tabelle in der (damit gesicherten)
/// Datenbank selbst, um Backup-Verwaltung und Datenbankinhalt sauber zu trennen.</summary>
public class BackupFileInfo
{
    public required string FileName { get; set; }

    public bool IsAutomatic { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public long SizeBytes { get; set; }
}
