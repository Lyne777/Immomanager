namespace Immomanager.Web.Models;

/// <summary>Ergebnis eines einzelnen Szenarios (Sensitivität oder gespeicherter Vergleich) inklusive
/// der zugrunde liegenden Abweichungen (Deltas) gegenüber der Basiskalkulation.</summary>
public class ScenarioResult
{
    public string Name { get; set; } = string.Empty;
    public decimal PurchasePriceDeltaPercent { get; set; }
    public decimal InterestRateDeltaPercentPoints { get; set; }
    public decimal RentDeltaPercent { get; set; }
    public DealCalculationResult Result { get; set; } = new();
}
