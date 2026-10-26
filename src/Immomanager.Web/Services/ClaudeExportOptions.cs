namespace Immomanager.Web.Services;

/// <summary>Bindung für den "ClaudeExport"-Abschnitt (appsettings.json bzw. Umgebungsvariablen, z. B.
/// ClaudeExport__Enabled=false).</summary>
public class ClaudeExportOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Lokale Uhrzeit (Europe/Berlin), zu der der tägliche Export startet.</summary>
    public int Hour { get; set; } = 3;

    /// <summary>Kopiert die in Immomanager hochgeladenen Unterlagen zusätzlich nach "dokumente/" im
    /// Export-Ordner. Standardmäßig aus: Liegen die Unterlagen ohnehin strukturiert in OneDrive, fände
    /// Claude sonst zwei Fassungen desselben Dokuments. Dann stehen in den Objekt-Dateien nur die Namen der
    /// hinterlegten Dokumente (ClaudeExport__MirrorDocuments=true schaltet die Kopien ein).</summary>
    public bool MirrorDocuments { get; set; }
}
