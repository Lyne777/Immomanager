namespace Immomanager.Web.Services;

/// <summary>Verwaltet den Anthropic API-Key/Modell zur Laufzeit über die "Einstellungen"-Seite,
/// als Alternative zum manuellen Editieren von appsettings.json (das im Docker-Image liegt und daher
/// weder im Dateisystem der NAS auffindbar ist noch Container-Updates übersteht).</summary>
public interface IAnthropicSettingsService
{
    /// <summary>Ob aktuell irgendein API-Key wirksam ist (aus appsettings.json, Umgebungsvariable
    /// oder der über die UI gespeicherten Datei - je nachdem, was zuletzt geladen wurde).</summary>
    bool IsApiKeyConfigured { get; }

    /// <summary>Aktuell wirksames Modell. Der API-Key selbst wird hier bewusst nicht zurückgegeben,
    /// damit ein einmal gespeichertes Secret nie erneut im UI angezeigt werden muss.</summary>
    string CurrentModel { get; }

    /// <summary>Speichert die Einstellungen dauerhaft im Datenverzeichnis und aktiviert sie sofort
    /// (ohne Programmneustart). Ein leerer <paramref name="apiKey"/> lässt den aktuell wirksamen
    /// Key unangetastet - so kann z. B. nur das Modell geändert werden, ohne den Key erneut
    /// eingeben zu müssen.</summary>
    Task SaveAsync(string? apiKey, string model);
}
