using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Eine Nebenkosten-/Betriebskostenabrechnung für ein Abrechnungsjahr, eindeutig je
/// Immobilie+Einheit+Jahr (Index siehe <see cref="Data.ApplicationDbContext"/>). "TotalCosts" ist
/// bewusst ein eigenständig gepflegtes Feld (der auf der echten Abrechnung ausgewiesene
/// Gesamtbetrag) statt einer aus den Einzelpositionen berechneten Summe - die Positionsliste
/// (<see cref="UtilityCostItem"/>) ist eine ergänzende Aufschlüsselung, die nicht zwingend jede
/// Position der Originalabrechnung einzeln erfasst.
/// <see cref="PropertyUnitId"/> ist bewusst nullable: null bedeutet eine gemeinsame Abrechnung
/// fürs ganze Objekt (z. B. wenn die Hausverwaltung nicht personalisiert abrechnet oder bei
/// Selbstverwaltung), gesetzt bedeutet eine einzelne, personalisierte Abrechnung dieser Einheit.
/// Beides kann nebeneinander existieren - das Objekt-Gesamt ist dann einfach die Summe aus allen
/// Abrechnungen des Jahres (siehe <see cref="UtilityService.CalculateKpi"/>). Garagen/Stellplätze
/// (<see cref="PropertyUnit.CountsTowardRentTarget"/> = false) haben in aller Regel keine eigene
/// Abrechnung, da sie nichts verbrauchen - das wird nicht erzwungen, es bleibt einfach leer.</summary>
public class UtilityStatement
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property? Property { get; set; }

    public int? PropertyUnitId { get; set; }

    [ForeignKey(nameof(PropertyUnitId))]
    public PropertyUnit? PropertyUnit { get; set; }

    [Range(2000, 2100)]
    public int Year { get; set; } = DateTime.Today.Year - 1;

    [Range(0, double.MaxValue)]
    public decimal TotalCosts { get; set; }

    /// <summary>Anzahl der Monate, die "TotalCosts" tatsächlich abdeckt - Standard 12 (volles
    /// Kalenderjahr). Im Kauf-/Verkaufsjahr deckt eine Abrechnung oft nur einen Teilzeitraum ab (z. B.
    /// 6 Monate); ohne diesen Wert würde eine reine Division durch 12 den €/Monat-Wert verfälschen.</summary>
    [Range(0.1, 24)]
    public decimal PeriodMonths { get; set; } = 12m;

    public bool IsCompleted { get; set; }

    public List<UtilityCostItem> Items { get; set; } = new();

    /// <summary>Hochgeladene Original-PDFs zu dieser Abrechnung - bewusst eine Liste statt eines
    /// einzelnen Felds, da manche Hausverwaltungen je Einheit eine eigene, personalisierte
    /// Abrechnung ausstellen statt einer gemeinsamen Abrechnung fürs ganze Objekt.</summary>
    public List<UtilityStatementDocument> Documents { get; set; } = new();
}
