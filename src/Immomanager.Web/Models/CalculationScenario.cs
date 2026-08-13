using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Ein gespeichertes "Was-wäre-wenn"-Szenario zu einer Ankaufsprüfung (z. B. "Worst Case"),
/// definiert als Abweichung (Delta) von Kaufpreis, Zinssatz und Miete gegenüber der Basiskalkulation.</summary>
public class CalculationScenario
{
    public int Id { get; set; }

    public int DealCalculationId { get; set; }

    [ForeignKey(nameof(DealCalculationId))]
    public DealCalculation? DealCalculation { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = "Szenario";

    [Range(-50, 50)]
    public decimal PurchasePriceDeltaPercent { get; set; }

    /// <summary>Absolute Abweichung in Prozentpunkten, auf jedes Darlehen gleichermaßen angewendet.</summary>
    [Range(-10, 10)]
    public decimal InterestRateDeltaPercentPoints { get; set; }

    [Range(-50, 50)]
    public decimal RentDeltaPercent { get; set; }
}
