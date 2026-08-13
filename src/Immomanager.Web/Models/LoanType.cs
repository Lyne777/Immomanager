namespace Immomanager.Web.Models;

/// <summary>Tilgungsform eines Darlehens - bestimmt, wie sich die Restschuld über die Zeit entwickelt
/// (Annuität: sinkt planmäßig; Endfällig: bleibt bis zur Fälligkeit konstant, da nur Zinsen gezahlt
/// werden). "Sonstiges" für Sonderformen (z. B. Bauspardarlehen in der Ansparphase), für die keine
/// automatische Restschuld-Berechnung angeboten wird.</summary>
public enum LoanType
{
    Annuitaet,
    Endfaellig,
    Sonstiges,
}
