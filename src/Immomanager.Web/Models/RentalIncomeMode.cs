namespace Immomanager.Web.Models;

/// <summary>Steuert, ob die Mieteinkünfte einer Kalkulation global (ein Betrag) oder je Einheit
/// (MFH-Kalkulator mit Einheiten-Tabelle) erfasst werden.</summary>
public enum RentalIncomeMode
{
    Global,
    UnitBased,
}
