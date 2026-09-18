namespace Immomanager.Web.Services;

/// <summary>Verwaltet die Bring!-Zugangsdaten/Zielliste zur Laufzeit über die "Küche"-Einstellungen,
/// analog zu <see cref="IAnthropicSettingsService"/>.</summary>
public interface IBringSettingsService
{
    /// <summary>Ob Email/Passwort hinterlegt sind.</summary>
    bool IsConfigured { get; }

    /// <summary>Ob zusätzlich eine Zielliste ausgewählt wurde.</summary>
    bool HasListSelected { get; }

    /// <summary>Hinterlegte Email - unkritisch, wird zur Bestätigung angezeigt (im Gegensatz zum
    /// Passwort, das nie wieder im Klartext ausgegeben wird).</summary>
    string? CurrentEmail { get; }

    string? CurrentListName { get; }

    /// <summary>Speichert Email/Passwort. Ein leeres Passwort lässt das aktuell hinterlegte
    /// unangetastet (z. B. um nur die Email zu korrigieren).</summary>
    Task SaveCredentialsAsync(string email, string? password);

    Task SaveSelectedListAsync(string listUuid, string listName);
}
