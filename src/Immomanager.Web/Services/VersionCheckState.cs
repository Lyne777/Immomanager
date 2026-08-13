namespace Immomanager.Web.Services;

/// <summary>Ergebnis der letzten Prüfung, ob auf GitHub eine neuere Version als die laufende
/// vorliegt (siehe <see cref="VersionCheckBackgroundService"/>). Als Singleton registriert, damit
/// alle Seiten denselben zuletzt ermittelten Stand lesen, ohne bei jedem Seitenaufruf selbst
/// nachzufragen.</summary>
public class VersionCheckState
{
    public bool IsUpdateAvailable { get; set; }
    public string? CurrentVersion { get; set; }
    public string? LatestVersion { get; set; }
    public DateTime? LastCheckedUtc { get; set; }
}
