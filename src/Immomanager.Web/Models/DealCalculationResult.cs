namespace Immomanager.Web.Models;

/// <summary>Vollständiges Berechnungsergebnis einer Ankaufsprüfung: Investitionssumme, Jahr-1-Kennzahlen
/// und die jahrgenaue Prognose über den gewählten Zeitraum.</summary>
public class DealCalculationResult
{
    public decimal TotalAcquisitionCosts { get; set; }
    public decimal TotalInvestment { get; set; }
    public decimal TotalLoanAmount { get; set; }
    public decimal EquityRequired { get; set; }

    public RenovationTaxCheck RenovationCheck { get; set; } = new();

    public decimal PricePerSqm { get; set; }
    public decimal PriceMultiplier { get; set; }
    public decimal GrossRentalYieldPercent { get; set; }
    public decimal NetRentalYieldPercent { get; set; }

    public decimal CashflowBeforeTaxMonthly { get; set; }
    public decimal CashflowAfterTaxMonthly { get; set; }
    public decimal EquityReturnPercent { get; set; }

    /// <summary>Um wie viele Prozentpunkte dürfte der Zins auf allen Darlehen maximal steigen, bevor der
    /// Cashflow nach Steuern in Jahr 1 negativ wird? Null = auch +20 Prozentpunkte werden noch verkraftet.</summary>
    public decimal? MaxSustainableInterestRateIncreasePoints { get; set; }

    public int? BreakEvenCashflowYear { get; set; }
    public int? BreakEvenTotalYear { get; set; }
    public int? FullyRepaidYear { get; set; }

    public List<YearlyProjectionRow> Projection { get; set; } = new();
}
