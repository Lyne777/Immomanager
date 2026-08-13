using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Eine einzelne Einheit (Wohnung/Gewerbe) innerhalb einer Immobilie (1:N). Die
/// Objekt-Summenwerte (Fläche, Kaltmiete, nicht umlegbare Kosten) ergeben sich aus der Summe aller
/// Einheiten (<see cref="Property.LivingAreaSqm"/> etc.), damit es keine getrennt gepflegten,
/// potenziell auseinanderlaufenden Aggregatwerte gibt.</summary>
public class PropertyUnit
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property? Property { get; set; }

    /// <summary>Bezeichnung der Einheit, z. B. "Whg. 1 OG links" oder "Einheit 1".</summary>
    [Required, StringLength(100)]
    public string Label { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal AreaSqm { get; set; }

    /// <summary>Aktuelle Nettokaltmiete pro Monat dieser Einheit.</summary>
    [Range(0, double.MaxValue)]
    public decimal ColdRentMonthly { get; set; }

    /// <summary>Nicht umlegbare Kosten pro Monat dieser Einheit.</summary>
    [Range(0, double.MaxValue)]
    public decimal NonAllocableCostsMonthly { get; set; }

    /// <summary>Ob Fläche und Kaltmiete dieser Einheit in die €/m²-Soll-Ist-Vergleichsrechnung
    /// (Soll-Miete-Tab) einfließen. Standardmäßig true; wird z. B. für Garagen/Stellplätze auf false
    /// gesetzt, da für sie kein Wohnflächen-Mietspiegel gilt und sie den €/m²-Wert sonst verzerren.</summary>
    public bool CountsTowardRentTarget { get; set; } = true;

    public List<Tenancy> Tenancies { get; set; } = new();

    public List<UnitDocument> Documents { get; set; } = new();

    /// <summary>Das aktuell laufende Mietverhältnis dieser Einheit, falls vorhanden.</summary>
    public Tenancy? CurrentTenancy => Tenancies
        .Where(t => t.IsCurrent)
        .OrderByDescending(t => t.MoveInDate)
        .FirstOrDefault();
}
