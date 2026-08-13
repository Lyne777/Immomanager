using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Eine Einheit innerhalb des MFH-Kalkulators (Modus B der Mieteinkünfte-Erfassung).</summary>
public class UnitCalculation
{
    public int Id { get; set; }

    public int DealCalculationId { get; set; }

    [ForeignKey(nameof(DealCalculationId))]
    public DealCalculation? DealCalculation { get; set; }

    [Required, StringLength(100)]
    public string UnitLabel { get; set; } = "Einheit";

    [Range(0, double.MaxValue)]
    public decimal AreaSqm { get; set; }

    /// <summary>Aktuelle Ist-Miete pro Monat.</summary>
    [Range(0, double.MaxValue)]
    public decimal CurrentRentMonthly { get; set; }

    /// <summary>Angestrebte Ziel-Miete pro Monat nach einer geplanten Mieterhöhung.</summary>
    [Range(0, double.MaxValue)]
    public decimal TargetRentMonthly { get; set; }

    /// <summary>Prognosejahr, ab dem die Ziel-Miete erreicht ist (Jahr 1 = sofort).</summary>
    [Range(1, 50)]
    public int TargetRentReachedInYear { get; set; } = 1;
}
