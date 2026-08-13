using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Ein Darlehen/eine Finanzierung, die zu einer Immobilie gehört (1:N).</summary>
public class Financing
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property? Property { get; set; }

    [Required, StringLength(200)]
    public string BankName { get; set; } = string.Empty;

    /// <summary>Ursprünglicher Darlehensbetrag.</summary>
    [Range(0, double.MaxValue)]
    public decimal OriginalLoanAmount { get; set; }

    /// <summary>Aktuelle Restschuld.</summary>
    [Range(0, double.MaxValue)]
    public decimal CurrentRemainingDebt { get; set; }

    /// <summary>Sollzins in Prozent p.a.</summary>
    [Range(0, 100)]
    public decimal InterestRatePercent { get; set; }

    /// <summary>Anfängliche Tilgung in Prozent p.a.</summary>
    [Range(0, 100)]
    public decimal InitialRepaymentRatePercent { get; set; }

    /// <summary>Monatliche Rate (Zins + Tilgung) in Euro.</summary>
    [Range(0, double.MaxValue)]
    public decimal MonthlyPayment { get; set; }

    public DateOnly FixedInterestEndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(10));

    /// <summary>Bereits getilgtes Kapital.</summary>
    public decimal PrincipalPaidToDate => OriginalLoanAmount - CurrentRemainingDebt;
}
