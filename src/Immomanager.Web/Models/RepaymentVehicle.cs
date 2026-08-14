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

    /// <summary>Aktuell angespartes Guthaben - entweder manuell gepflegt oder per "Sparstand
    /// berechnen" aus "CalculationStartDate"/"CalculationStartValue" plus seitdem angesammelten
    /// Monatsbeiträgen vorgeschlagen. Bewusst kein Range-Minimum: kurz nach Vertragsbeginn kann der
    /// Stand (z. B. wegen einer Abschlussgebühr) noch negativ sein.</summary>
    public decimal CurrentValue { get; set; }

    /// <summary>Datum, zu dem "CalculationStartValue" als Sparstand galt - Ausgangspunkt für die
    /// Sparstand-Berechnung (i. d. R. Vertrags-/Beitragsbeginn).</summary>
    public DateOnly CalculationStartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Sparstand zum "CalculationStartDate" - kann bewusst negativ sein, um z. B. eine zu
    /// Vertragsbeginn abgezogene Abschlussgebühr abzubilden, die erst durch spätere Beiträge
    /// ausgeglichen wird.</summary>
    public decimal CalculationStartValue { get; set; }

    public DateOnly ContractEndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(10));
}
