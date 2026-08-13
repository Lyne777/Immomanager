using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

/// <summary>Anzeigenamen und Gruppierung der Betriebskostenarten für die Drill-Down-Ansicht (Ebene 1
/// = Hauptgruppe, Ebene 2 = einzelne Kategorie).</summary>
public static class UtilityCostCatalog
{
    public static readonly Dictionary<UtilityCostCategory, string> CategoryDisplayNames = new()
    {
        [UtilityCostCategory.HeizungWarmwasser] = "Heizung/Warmwasser",
        [UtilityCostCategory.Grundsteuer] = "Grundsteuer",
        [UtilityCostCategory.Gebaeudeversicherung] = "Gebäudeversicherung",
        [UtilityCostCategory.Muellabfuhr] = "Müllabfuhr",
        [UtilityCostCategory.Allgemeinstrom] = "Allgemeinstrom",
        [UtilityCostCategory.WasserAbwasser] = "Wasser/Abwasser",
        [UtilityCostCategory.Hausreinigung] = "Hausreinigung",
        [UtilityCostCategory.Gartenpflege] = "Gartenpflege",
        [UtilityCostCategory.Schornsteinfeger] = "Schornsteinfeger",
        [UtilityCostCategory.SonstigeBetrKV] = "Sonstige BetrKV-Position",
    };

    public static readonly Dictionary<UtilityCostGroup, string> GroupDisplayNames = new()
    {
        [UtilityCostGroup.WarmkostenHeizung] = "Warmkosten / Heizung",
        [UtilityCostGroup.KommunaleAbgaben] = "Kommunale Abgaben",
        [UtilityCostGroup.BetriebUndPflege] = "Betrieb & Pflege",
        [UtilityCostGroup.Versicherungen] = "Versicherungen",
    };

    public static readonly Dictionary<UtilityCostCategory, UtilityCostGroup> CategoryGroups = new()
    {
        [UtilityCostCategory.HeizungWarmwasser] = UtilityCostGroup.WarmkostenHeizung,
        [UtilityCostCategory.Grundsteuer] = UtilityCostGroup.KommunaleAbgaben,
        [UtilityCostCategory.Muellabfuhr] = UtilityCostGroup.KommunaleAbgaben,
        [UtilityCostCategory.WasserAbwasser] = UtilityCostGroup.KommunaleAbgaben,
        [UtilityCostCategory.Schornsteinfeger] = UtilityCostGroup.KommunaleAbgaben,
        [UtilityCostCategory.Gebaeudeversicherung] = UtilityCostGroup.Versicherungen,
        [UtilityCostCategory.Allgemeinstrom] = UtilityCostGroup.BetriebUndPflege,
        [UtilityCostCategory.Hausreinigung] = UtilityCostGroup.BetriebUndPflege,
        [UtilityCostCategory.Gartenpflege] = UtilityCostGroup.BetriebUndPflege,
        [UtilityCostCategory.SonstigeBetrKV] = UtilityCostGroup.BetriebUndPflege,
    };
}
