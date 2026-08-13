using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Eine Nebenkosten-/Betriebskostenabrechnung einer Immobilie für ein Abrechnungsjahr
/// (1:N, eindeutig je Immobilie+Jahr). "TotalCosts" ist bewusst ein eigenständig gepflegtes Feld
/// (der auf der echten Abrechnung ausgewiesene Gesamtbetrag) statt einer aus den Einzelpositionen
/// berechneten Summe - die Positionsliste (<see cref="UtilityCostItem"/>) ist eine ergänzende
/// Aufschlüsselung, die nicht zwingend jede Position der Originalabrechnung einzeln erfasst.</summary>
public class UtilityStatement
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property? Property { get; set; }

    [Range(2000, 2100)]
    public int Year { get; set; } = DateTime.Today.Year - 1;

    [Range(0, double.MaxValue)]
    public decimal TotalCosts { get; set; }

    [StringLength(500)]
    public string? PdfFilePath { get; set; }

    [StringLength(200)]
    public string? PdfFileName { get; set; }

    public bool IsCompleted { get; set; }

    public List<UtilityCostItem> Items { get; set; } = new();
}
