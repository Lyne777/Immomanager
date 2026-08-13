namespace Immomanager.Web.Models;

/// <summary>Art des Gesamtprojekts, für projektübergreifende Lernwerte (z. B. "Bad-Sanierung kostet
/// durchschnittlich 1.100 €/m²"), unabhängig von den einzelnen Gewerken innerhalb des Projekts.</summary>
public enum RenovationCategory
{
    Badsanierung,
    Kuechensanierung,
    Vollsanierung,
    Bodensanierung,
    Fassadensanierung,
    Dachsanierung,
    Fenstersanierung,
    Heizungssanierung,
    Sonstiges,
}
