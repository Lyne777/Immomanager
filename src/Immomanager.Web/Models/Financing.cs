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

    /// <summary>Tilgungsform - bestimmt u. a., ob/wie sich aus <see cref="CalculationStartDate"/> und
    /// <see cref="MonthlyPayment"/> eine aktuelle Restschuld berechnen lässt.</summary>
    public LoanType LoanType { get; set; } = LoanType.Annuitaet;

    /// <summary>Datum, zu dem "OriginalLoanAmount" als Restschuld galt (i. d. R. das
    /// Auszahlungsdatum) - Ausgangspunkt für die Restschuld-Berechnung in
    /// <see cref="Services.LoanAmortizationCalculator"/>.</summary>
    public DateOnly CalculationStartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Bereits getilgtes Kapital.</summary>
    public decimal PrincipalPaidToDate => OriginalLoanAmount - CurrentRemainingDebt;

    public List<RepaymentVehicle> RepaymentVehicles { get; set; } = new();

    /// <summary>"Wirtschaftliche" Restschuld nach Abzug bereits angesparter Tilgungsersatzmittel
    /// (z. B. Bausparguthaben) - rein informativ. Rechtlich/für LTV-Zwecke bleibt weiterhin die volle
    /// "CurrentRemainingDebt" maßgeblich, da diese bis zur tatsächlichen Ablösung in voller Höhe
    /// geschuldet ist.</summary>
    public decimal NetRemainingDebt => CurrentRemainingDebt - RepaymentVehicles.Sum(r => r.CurrentValue);
}
