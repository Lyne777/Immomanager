namespace Immomanager.Web.Services;

/// <summary>Merkt sich Eingaben im Küchenbereich über Sitzungen hinweg (Datei im Datenverzeichnis), damit
/// z. B. die Wünsche/Ausschlüsse beim nächsten Speiseplan nicht neu getippt werden müssen.</summary>
public interface IKuecheSettingsService
{
    /// <summary>Maximale Länge der Wünsche/Ausschlüsse - gilt für Eingabefeld, Speicherung und Prompt.</summary>
    public const int MaxNotesLength = 2000;

    /// <summary>Zuletzt für die Speiseplan-Generierung verwendete Wünsche/Ausschlüsse.</summary>
    string? LastNotes { get; }

    Task SaveLastNotesAsync(string? notes);
}
