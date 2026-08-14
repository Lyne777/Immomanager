using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Ein Tilgungsersatzmittel (z. B. Bausparvertrag, Lebensversicherung) zu einem endfälligen
/// Darlehen (1:N an Financing) - spart bis zur Fälligkeit einen Betrag an, der das Darlehen dann
/// ablöst. "CurrentValue" wird wie "Financing.CurrentRemainingDebt" manuell periodisch gepflegt
/// (z. B. anhand des jährlichen Bausparkassen-Kontoauszugs).</summary>
public class RepaymentVehicle
{
    public int Id { get; set; }

    public int FinancingId { get; set; }

    [ForeignKey(nameof(FinancingId))]
    public Financing? Financing { get; set; }

    /// <summary>Produkt/Anbieter, z. B. "Ankommer 6 (Bausparvertrag)".</summary>
    [Required, StringLength(200)]
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Zielbetrag (z. B. Bausparsumme), der das gekoppelte Darlehen ablösen soll.</summary>
    [Range(0, double.MaxValue)]
    public decimal TargetAmount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MonthlyContribution { get; set; }

    /// <summary>Aktuell angespartes Guthaben.</summary>
    [Range(0, double.MaxValue)]
    public decimal CurrentValue { get; set; }

    public DateOnly ContractEndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(10));
}
