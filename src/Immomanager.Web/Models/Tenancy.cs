using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Ein Mietverhältnis zu einer Einheit (1:N - eine Einheit kann über die Zeit mehrere
/// Mietverhältnisse haben: aktuelles + historische). "IsCurrent" wird bewusst nicht gespeichert,
/// sondern aus MoveOutDate abgeleitet, damit es nicht getrennt vom eigentlichen Auszugsdatum
/// gepflegt werden muss und nie aus dem Takt geraten kann.</summary>
public class Tenancy
{
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

    /// <summary>Vertragliche Kaltmiete - bewusst unabhängig von PropertyUnit.ColdRentMonthly
    /// gespeichert, da hier auch der historische, zum jeweiligen Mietverhältnis passende Wert
    /// erhalten bleiben muss, nachdem PropertyUnit.ColdRentMonthly für einen neuen Mieter geändert wurde.</summary>
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

    /// <summary>Ob das Mietverhältnis aktuell läuft (kein Auszugsdatum oder Auszug in der Zukunft).</summary>
    public bool IsCurrent => !MoveOutDate.HasValue || MoveOutDate.Value >= DateOnly.FromDateTime(DateTime.Today);
}
