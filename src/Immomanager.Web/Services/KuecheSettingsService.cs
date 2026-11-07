using System.Text.Json;

namespace Immomanager.Web.Services;

/// <summary>Als Singleton registriert: Zustand liegt in "kueche-settings.json" im Datenverzeichnis und
/// wird einmal gelesen und beim Speichern im Speicher mitgeführt (ein einzelner Haushalt, kein
/// paralleler Schreibzugriff von außen).</summary>
public class KuecheSettingsService : IKuecheSettingsService
{
    private readonly string _settingsFilePath;
    private readonly ILogger<KuecheSettingsService> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private string? _lastNotes;

    public KuecheSettingsService(StorageOptions storageOptions, ILogger<KuecheSettingsService> logger)
    {
        _logger = logger;
        _settingsFilePath = Path.Combine(storageOptions.DataDirectoryAbsolute, "kueche-settings.json");
        _lastNotes = Load();
    }

    public string? LastNotes => _lastNotes;

    public async Task SaveLastNotesAsync(string? notes)
    {
        var normalized = Normalize(notes);

        await _writeLock.WaitAsync();
        try
        {
            if (string.Equals(_lastNotes, normalized, StringComparison.Ordinal))
            {
                return;
            }

            var json = JsonSerializer.Serialize(new PersistedSettings(normalized), new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsFilePath, json);
            _lastNotes = normalized;
        }
        catch (IOException ex)
        {
            // Nur Komfortfunktion - ein Schreibfehler darf die Generierung nicht verhindern.
            _logger.LogWarning(ex, "Küchen-Einstellungen konnten nicht gespeichert werden.");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>Kürzt auf <see cref="IKuecheSettingsService.MaxNotesLength"/>; leer wird zu null.</summary>
    public static string? Normalize(string? notes)
    {
        var trimmed = notes?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > IKuecheSettingsService.MaxNotesLength
            ? trimmed[..IKuecheSettingsService.MaxNotesLength]
            : trimmed;
    }

    private string? Load()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return null;
        }

        try
        {
            var persisted = JsonSerializer.Deserialize<PersistedSettings>(File.ReadAllText(_settingsFilePath));
            return Normalize(persisted?.LastNotes);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogWarning(ex, "Küchen-Einstellungen konnten nicht gelesen werden, starte ohne gespeicherte Wünsche.");
            return null;
        }
    }

    private sealed record PersistedSettings(string? LastNotes);
}
