using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Ein Renovierungs-/Sanierungsprojekt an einer Immobilie (1:N), z. B. "Bad-Modernisierung OG".
/// Die Ist-Kosten ergeben sich aus der Summe der einzelnen Gewerk-Positionen (<see cref="LineItems"/>),
/// damit Kalkulator/Lernwerte immer auf denselben, granularen Zahlen basieren wie die Detailauswertung.</summary>
public class RenovationProject
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property? Property { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Art des Projekts für projektübergreifende Lernwerte (z. B. Badsanierung).</summary>
    public RenovationCategory Category { get; set; } = RenovationCategory.Sonstiges;

    public RenovationStatus Status { get; set; } = RenovationStatus.Geplant;

    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public DateOnly? EndDate { get; set; }

    /// <summary>Von der Renovierung betroffene Fläche in m² (z. B. Bad = 8 m²).</summary>
    [Range(0, double.MaxValue)]
    public decimal AreaSqm { get; set; }

    /// <summary>Geplante Gesamtkosten (Budget) für den Soll/Ist-Vergleich.</summary>
    [Range(0, double.MaxValue)]
    public decimal PlannedTotalCost { get; set; }

    public string? Notes { get; set; }

    public List<RenovationLineItem> LineItems { get; set; } = new();

    /// <summary>Ist-Gesamtkosten = Summe aller Gewerk-Positionen (Material + Lohn).</summary>
    public decimal ActualTotalCost => LineItems.Sum(li => li.TotalCost);

    public decimal BudgetVariance => ActualTotalCost - PlannedTotalCost;

    /// <summary>Ist-Kosten pro m² für das Gesamtprojekt.</summary>
    public decimal CostPerSqm => AreaSqm > 0 ? ActualTotalCost / AreaSqm : 0;
}
