using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Eine einzelne Kostenposition innerhalb einer Nebenkostenabrechnung
/// (<see cref="UtilityStatement"/>), z. B. "Gemeindesteuern Stadt Siershahn" unter der Kategorie
/// Grundsteuer.</summary>
public class UtilityCostItem
{
    public int Id { get; set; }

    public int UtilityStatementId { get; set; }

    [ForeignKey(nameof(UtilityStatementId))]
    public UtilityStatement? UtilityStatement { get; set; }

    public UtilityCostCategory Category { get; set; }

    [Required, StringLength(300)]
    public string Description { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }
}
