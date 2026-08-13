using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Ein Darlehen innerhalb einer Ankaufsprüfung (z. B. Hauptdarlehen, KfW, Nachrangdarlehen).</summary>
public class LoanCalculation
{
    public int Id { get; set; }

    public int DealCalculationId { get; set; }

    [ForeignKey(nameof(DealCalculationId))]
    public DealCalculation? DealCalculation { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = "Hauptdarlehen";

    [Range(0, double.MaxValue)]
    public decimal LoanAmount { get; set; }

    [Range(0, 20)]
    public decimal InterestRatePercent { get; set; } = 3.5m;

    [Range(0, 20)]
    public decimal InitialRepaymentRatePercent { get; set; } = 2m;

    /// <summary>Sondertilgung pro Jahr (€), wird am Jahresende auf die Restschuld angerechnet.</summary>
    [Range(0, double.MaxValue)]
    public decimal AnnualSpecialRepayment { get; set; }

    [Range(1, 40)]
    public int FixedInterestYears { get; set; } = 10;
}
