namespace Immomanager.Web.Models;

/// <summary>Historischer Erfahrungswert für eine Projektkategorie (z. B. Badsanierung), aggregiert aus
/// den Ist-Gesamtkosten aller abgeschlossenen Projekte dieser Kategorie über das gesamte Portfolio.</summary>
public class CategoryLearningStat
{
    public RenovationCategory Category { get; set; }
    public int SampleCount { get; set; }

    public decimal TotalArea { get; set; }
    public decimal TotalCost { get; set; }

    public decimal MinCostPerSqm { get; set; }
    public decimal MaxCostPerSqm { get; set; }

    public decimal AverageCostPerSqm => TotalArea > 0 ? TotalCost / TotalArea : 0;
}
