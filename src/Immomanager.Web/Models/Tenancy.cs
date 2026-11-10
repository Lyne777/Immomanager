using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Ein Mietverhältnis zu einer Einheit (1:N - eine Einheit kann über die Zeit mehrere
/// Mietverhältnisse haben: aktuelles + historische). "IsCurrent" wird bewusst nicht gespeichert,
/// sondern aus Einzugs- und Auszugsdatum abgeleitet, damit es nicht getrennt von den eigentlichen
/// Daten gepflegt werden muss und nie aus dem Takt geraten kann. Die Kaltmiete des laufenden
/// Mietverhältnisses ist zugleich die Kaltmiete der Einheit (<see cref="PropertyUnit.ColdRentMonthly"/>)
/// und damit die einzige gepflegte Mietangabe.</summary>
public class Tenancy
{
    /// <summary>Platzhalter-Mietername, mit dem ein Mietvertrags-Upload ein Mietverhältnis anlegt, bis
    /// es ausgewertet bzw. von Hand ausgefüllt wurde (siehe <see cref="IsPendingAnalysis"/>).</summary>
    public const string PendingTenantName = "(noch nicht ausgefüllt - Mietvertrag wartet auf Auswertung)";

    public int Id { get; set; }

    public int PropertyUnitId { get; set; }

    [ForeignKey(nameof(PropertyUnitId))]
    public PropertyUnit? PropertyUnit { get; set; }

    [Required, StringLength(200)]
    public string TenantName { get; set; } = string.Empty;

    [StringLength(200)]
    public string? TenantEmail { get; set; }

    [StringLength(50)]
    public string? TenantPhone { get; set; }

    public DateOnly MoveInDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Auszugsdatum - null bedeutet unbefristet/noch laufend.</summary>
    public DateOnly? MoveOutDate { get; set; }

    /// <summary>Vertragliche Kaltmiete. Einzige gepflegte Mietangabe: Die Kaltmiete der Einheit und alle
    /// davon abhängigen Kennzahlen (Cashflow, Rendite, Ertragswert ...) ergeben sich aus dem Mietverhältnis,
    /// das gerade läuft. Frühere Mietverhältnisse behalten ihren damaligen Wert (Mietentwicklung).</summary>
    [Range(0, double.MaxValue)]
    public decimal ColdRentMonthly { get; set; }

    /// <summary>Monatliche Nebenkostenvorauszahlung des Mieters.</summary>
    [Range(0, double.MaxValue)]
    public decimal AdvancePaymentMonthly { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? SecurityDeposit { get; set; }

    [StringLength(500)]
    public string? PdfFilePath { get; set; }

    [StringLength(200)]
    public string? PdfFileName { get; set; }

    public string? Notes { get; set; }

    /// <summary>Ob dieses Mietverhältnis nur ein Platzhalter aus einem Mietvertrags-Upload ist, der noch
    /// nicht ausgewertet wurde. Solche Einträge tragen weder Miete noch Mieter bei - sonst würde ein Upload
    /// (Einzugsdatum = heute, Miete 0) die Miete des tatsächlichen Mieters verdrängen.</summary>
    public bool IsPendingAnalysis => TenantName == PendingTenantName;

    /// <summary>Ob das Mietverhältnis am angegebenen Tag lief: Einzug erreicht, Auszug (falls gesetzt) noch
    /// nicht überschritten. Nicht ausgewertete Platzhalter zählen nie als laufend.</summary>
    public bool IsActiveOn(DateOnly date) =>
        !IsPendingAnalysis && MoveInDate <= date && (!MoveOutDate.HasValue || MoveOutDate.Value >= date);

    /// <summary>Ob das Mietverhältnis aktuell läuft (Einzug erreicht, kein Auszugsdatum oder Auszug noch
    /// nicht überschritten).</summary>
    public bool IsCurrent => IsActiveOn(DateOnly.FromDateTime(DateTime.Today));
}
