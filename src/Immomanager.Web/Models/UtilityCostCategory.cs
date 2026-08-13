namespace Immomanager.Web.Models;

/// <summary>Betriebskostenart gemäß Betriebskostenverordnung (BetrKV) - die einzelne, konkrete
/// Kostenposition einer Nebenkostenabrechnung. Für die Drill-Down-Ansicht werden diese Kategorien
/// zu vier Hauptgruppen zusammengefasst (<see cref="Services.UtilityCostCatalog"/>).</summary>
public enum UtilityCostCategory
{
    HeizungWarmwasser,
    Grundsteuer,
    Gebaeudeversicherung,
    Muellabfuhr,
    Allgemeinstrom,
    WasserAbwasser,
    Hausreinigung,
    Gartenpflege,
    Schornsteinfeger,
    SonstigeBetrKV,
}
