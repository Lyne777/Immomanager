using System.Text.Json;

namespace Immomanager.Web.Services;

/// <summary>Zustand des Exports für Claude: Sperre gegen parallele Läufe (nächtlicher Dienst vs. Button
/// "Jetzt exportieren") und das Ergebnis des letzten Laufs. Als Singleton registriert und in
/// "claude-export-state.json" im Datenverzeichnis persistiert - der Hintergrunddienst braucht den
/// Zeitpunkt des letzten erfolgreichen Exports nach einem Neustart, um bei Bedarf nachzuholen.</summary>
public class ClaudeExportState
{
    private readonly string _stateFilePath;
    private readonly ILogger<ClaudeExportState> _logger;

    public ClaudeExportState(StorageOptions storageOptions, ILogger<ClaudeExportState> logger)
    {
        _logger = logger;
        _stateFilePath = Path.Combine(storageOptions.DataDirectoryAbsolute, "claude-export-state.json");
        Load();
    }

    public SemaphoreSlim RunLock { get; } = new(1, 1);

    public bool IsRunning => RunLock.CurrentCount == 0;

    public DateTime? LastSuccessUtc { get; private set; }

    public string? LastMessage { get; private set; }

    public bool LastRunFailed { get; private set; }

    public int ObjectCount { get; private set; }

    public int DocumentCount { get; private set; }

    public void RecordSuccess(int objectCount, int documentCount, bool documentsCopied)
    {
        LastSuccessUtc = DateTime.UtcNow;
        LastRunFailed = false;
        ObjectCount = objectCount;
        DocumentCount = documentCount;
        var documents = $"{documentCount} {(documentCount == 1 ? "Dokument" : "Dokumente")}";
        LastMessage = documentsCopied
            ? $"{objectCount} {(objectCount == 1 ? "Objekt" : "Objekte")} und {documents} exportiert."
            : $"{objectCount} {(objectCount == 1 ? "Objekt" : "Objekte")} exportiert, {documents} nur aufgelistet (nicht kopiert).";
        Save();
    }

    public void RecordFailure(string message)
    {
        LastRunFailed = true;
        LastMessage = message;
        Save();
    }

    /// <summary>Zeitzone für Uhrzeit-Berechnung und Zeitstempel im Export. Im Docker-Container steht
    /// "Europe/Berlin" zur Verfügung (tzdata im Image), unter Windows heißt sie anders - ohne Treffer
    /// fällt es auf die lokale Systemzeit zurück statt zu scheitern.</summary>
    public static TimeZoneInfo ResolveTimeZone()
    {
        foreach (var id in new[] { "Europe/Berlin", "W. Europe Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }

        return TimeZoneInfo.Local;
    }

    private void Load()
    {
        if (!File.Exists(_stateFilePath))
        {
            return;
        }

        try
        {
            var persisted = JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(_stateFilePath));
            if (persisted is null)
            {
                return;
            }

            LastSuccessUtc = persisted.LastSuccessUtc;
            LastMessage = persisted.LastMessage;
            LastRunFailed = persisted.LastRunFailed;
            ObjectCount = persisted.ObjectCount;
            DocumentCount = persisted.DocumentCount;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogWarning(ex, "Export-Zustand konnte nicht gelesen werden, starte ohne Verlauf.");
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(
                new PersistedState(LastSuccessUtc, LastMessage, LastRunFailed, ObjectCount, DocumentCount));
            File.WriteAllText(_stateFilePath, json);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Export-Zustand konnte nicht gespeichert werden.");
        }
    }

    private sealed record PersistedState(DateTime? LastSuccessUtc, string? LastMessage, bool LastRunFailed, int ObjectCount, int DocumentCount);
}
