namespace Immomanager.Web.Models;

/// <summary>Kostenaufschlüsselung eines einzelnen Renovierungsprojekts, gruppiert nach Gewerk
/// (z. B. "Bodenarbeiten: 82,50 €/m², davon X € Material, Y € Lohn").</summary>
public class ProjectTradeBreakdown
{
    public RenovationTrade Trade { get; set; }
    public string DominantUnit { get; set; } = "m²";

    public decimal Quantity { get; set; }
    public decimal MaterialCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal TotalCost => MaterialCost + LaborCost;

    public decimal CostPerUnit => Quantity > 0 ? TotalCost / Quantity : 0;
}
