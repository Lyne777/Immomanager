using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Die geplante (Soll-)Nettokaltmiete pro m² und Monat für ein Quartal einer Immobilie,
/// zum späteren Abgleich gegen die tatsächliche (Ist-)Miete.</summary>
public class RentTarget
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property? Property { get; set; }

    [Range(2000, 2100)]
    public int Year { get; set; } = DateTime.Today.Year;

    /// <summary>1 = Q1 (Jan-Mär) ... 4 = Q4 (Okt-Dez).</summary>
    [Range(1, 4)]
    public int Quarter { get; set; } = 1;

    /// <summary>Soll-Nettokaltmiete in €/m² pro Monat.</summary>
    [Range(0, double.MaxValue)]
    public decimal TargetRentPerSqm { get; set; }
}
