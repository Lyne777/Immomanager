using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Eine einzelne Gewerk-/Kostenposition innerhalb eines Renovierungsprojekts,
/// z. B. "Vinylboden inkl. Trittschalldämmung verlegt", 45 m², Material 1.200 €, Lohn 1.800 €.</summary>
public class RenovationLineItem
{
    public int Id { get; set; }

    public int RenovationProjectId { get; set; }

    [ForeignKey(nameof(RenovationProjectId))]
    public RenovationProject? RenovationProject { get; set; }

    public RenovationTrade Trade { get; set; } = RenovationTrade.Sonstiges;

    [Required, StringLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Betroffene Menge, z. B. 45 (m²) oder 3 (Stück).</summary>
    [Range(0, double.MaxValue)]
    public decimal Quantity { get; set; } = 1;

    /// <summary>Einheit der Menge, z. B. "m²", "Stück", "pauschal".</summary>
    [Required, StringLength(20)]
    public string Unit { get; set; } = "m²";

    [Range(0, double.MaxValue)]
    public decimal MaterialCost { get; set; }

    [Range(0, double.MaxValue)]
    public decimal LaborCost { get; set; }

    /// <summary>Optionale Zeiterfassung für Eigenleistung (nicht Teil der Kostenrechnung).</summary>
    [Range(0, 10000)]
    public decimal? SelfLaborHours { get; set; }

    public decimal TotalCost => MaterialCost + LaborCost;

    public decimal CostPerUnit => Quantity > 0 ? TotalCost / Quantity : 0;
}
