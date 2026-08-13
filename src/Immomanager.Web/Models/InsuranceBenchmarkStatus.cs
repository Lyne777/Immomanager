namespace Immomanager.Web.Models;

/// <summary>Einordnung des Jahresbeitrags pro Einheit gegenüber dem Richtwert-Korridor.</summary>
public enum InsuranceBenchmarkStatus
{
    /// <summary>Kein Jahresbeitrag oder keine Einheiten hinterlegt - keine Aussage möglich.</summary>
    NichtErmittelbar,

    /// <summary>Ungewöhnlich günstig - Hinweis auf mögliche Deckungslücken.</summary>
    UngewoehnlichGuenstig,

    ImRahmen,

    ZuTeuer,
}
