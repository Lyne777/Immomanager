namespace Immomanager.Web.Models;

/// <summary>Ergebnis des Renovierungs-Rechners: Kostenschätzung für ein neues Vorhaben auf Basis
/// historischer Erfahrungswerte (Gewerk- oder Kategorie-Durchschnitt).</summary>
public class RenovationEstimate
{
    public string Basis { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;

    public decimal AverageCostPerUnit { get; set; }
    public decimal MinCostPerUnit { get; set; }
    public decimal MaxCostPerUnit { get; set; }

    public decimal EstimatedCost => Quantity * AverageCostPerUnit;
    public decimal EstimatedCostLow => Quantity * MinCostPerUnit;
    public decimal EstimatedCostHigh => Quantity * MaxCostPerUnit;

    public int SampleCount { get; set; }
    public bool HasData => SampleCount > 0;
}
