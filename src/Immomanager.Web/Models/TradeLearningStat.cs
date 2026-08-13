namespace Immomanager.Web.Models;

/// <summary>Historischer Erfahrungswert für ein Gewerk, aggregiert aus allen Kostenpositionen
/// abgeschlossener Renovierungsprojekte über das gesamte Portfolio hinweg.</summary>
public class TradeLearningStat
{
    public RenovationTrade Trade { get; set; }
    public int SampleCount { get; set; }
    public string DominantUnit { get; set; } = "m²";

    public decimal TotalQuantity { get; set; }
    public decimal TotalMaterialCost { get; set; }
    public decimal TotalLaborCost { get; set; }
    public decimal TotalCost { get; set; }

    public decimal MinCostPerUnit { get; set; }
    public decimal MaxCostPerUnit { get; set; }

    /// <summary>Mengengewichteter Durchschnitt (Gesamtkosten / Gesamtmenge), robuster als ein
    /// einfacher Mittelwert einzelner Positionspreise.</summary>
    public decimal AverageCostPerUnit => TotalQuantity > 0 ? TotalCost / TotalQuantity : 0;

    public decimal AverageMaterialCostPerUnit => TotalQuantity > 0 ? TotalMaterialCost / TotalQuantity : 0;

    public decimal AverageLaborCostPerUnit => TotalQuantity > 0 ? TotalLaborCost / TotalQuantity : 0;
}
