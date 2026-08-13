namespace Immomanager.Web.Models;

/// <summary>Eine Zeile der jahrgenauen Prognose (Jahr 1 bis 30-50) einer Ankaufsprüfung.</summary>
public class YearlyProjectionRow
{
    public int Year { get; set; }

    public decimal GrossRentAnnual { get; set; }

    /// <summary>Alle zahlungswirksamen Bewirtschaftungskosten (nicht umlegbar + Instandhaltung + Mietausfallwagnis).</summary>
    public decimal OperatingCostsAnnual { get; set; }

    /// <summary>Steuerlich abzugsfähiger Anteil der Bewirtschaftungskosten (Werbungskosten).</summary>
    public decimal DeductibleOperatingCosts { get; set; }

    public decimal InterestPaid { get; set; }
    public decimal PrincipalPaid { get; set; }
    public decimal DebtServiceAnnual => InterestPaid + PrincipalPaid;
    public decimal RemainingDebt { get; set; }

    public decimal AfaAmount { get; set; }

    /// <summary>Sofort abzugsfähiger Sanierungsaufwand (nur Jahr 1, sofern unter der 15%-Grenze und keine Denkmal-AfA).</summary>
    public decimal ImmediateDeductibleRenovation { get; set; }

    public decimal TaxableIncome { get; set; }
    public decimal TaxAmount { get; set; }

    public decimal CashflowBeforeTax { get; set; }
    public decimal CashflowAfterTax { get; set; }
    public decimal CumulativeCashflowAfterTax { get; set; }

    public decimal PropertyValue { get; set; }
    public decimal NetWorth { get; set; }
}
