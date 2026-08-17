namespace Immomanager.Web.Models;

/// <summary>Berechnete Kennzahlen für eine einzelne Immobilie, wahlweise für das Gesamtobjekt oder den eigenen Anteil.</summary>
public class PropertyKpi
{
    public int PropertyId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public ViewMode ViewMode { get; set; }
    public decimal SharePercent { get; set; }

    public decimal PurchasePrice { get; set; }
    public decimal TotalAcquisitionCosts { get; set; }
    public decimal TotalInvestment { get; set; }
    public decimal CurrentMarketValue { get; set; }

    public decimal ColdRentMonthly { get; set; }
    public decimal ColdRentAnnual { get; set; }
    public decimal NonAllocableCostsMonthly { get; set; }
    public decimal NonAllocableCostsAnnual { get; set; }

    public decimal TotalRemainingDebt { get; set; }
    public decimal TotalMonthlyDebtService { get; set; }
    public decimal PrincipalPaidToDate { get; set; }
    public decimal EquityInvested { get; set; }
    public decimal CurrentEquityValue { get; set; }

    public decimal CashflowMonthly { get; set; }
    public decimal CashflowAnnual { get; set; }

    /// <summary>Bruttomietrendite in %: (Jahreskaltmiete / Kaufpreis) * 100</summary>
    public decimal GrossRentalYieldPercent { get; set; }

    /// <summary>Nettomietrendite in %: ((Jahreskaltmiete - nicht umlegbare Kosten) / Gesamtkaufkosten) * 100</summary>
    public decimal NetRentalYieldPercent { get; set; }

    /// <summary>Cash-on-Cash Return / Eigenkapitalrendite in %: (Jahres-Cashflow / eingesetztes Eigenkapital) * 100</summary>
    public decimal CashOnCashReturnPercent { get; set; }

    /// <summary>ROI in %: Wertsteigerung + Tilgung im Verhältnis zum eingesetzten Eigenkapital.</summary>
    public decimal RoiPercent { get; set; }

    /// <summary>Loan-to-Value in %: (Restschuld / Marktwert) * 100</summary>
    public decimal LoanToValuePercent { get; set; }

    /// <summary>Vereinfachter Ertragswert (Jahresnettokaltmiete × Vervielfältiger). Null, solange kein
    /// Multiplikator in den Stammdaten hinterlegt ist.</summary>
    public decimal? EstimatedIncomeValue { get; set; }

    /// <summary>Ertragswert zum Kaufzeitpunkt (auf Basis der bei Kauf hinterlegten Kaltmiete), für die
    /// Entwicklungs-Darstellung. Null, solange Multiplikator oder Kaltmiete bei Kauf fehlen.</summary>
    public decimal? EstimatedIncomeValueAtPurchase { get; set; }
}
