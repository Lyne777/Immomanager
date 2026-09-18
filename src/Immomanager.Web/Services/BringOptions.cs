namespace Immomanager.Web.Services;

/// <summary>Bindung für den "Bring"-Abschnitt der bring-settings.json im Datenverzeichnis (analog zu
/// <see cref="AnthropicOptions"/>). Nutzt die inoffizielle Bring!-API (siehe <see cref="BringService"/>)
/// mit den eigenen Zugangsdaten des Nutzers - rein optionale Zusatzfunktion, PDF-Export bleibt der
/// zuverlässige Weg.</summary>
public class BringOptions
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>Die vom Nutzer ausgewählte Ziel-Liste (uuid) in seinem Bring!-Konto.</summary>
    public string? ListUuid { get; set; }

    public string? ListName { get; set; }
}
