namespace Immomanager.Web.Models;

/// <summary>Aggregierte Kennzahlen über das gesamte Portfolio.</summary>
public class PortfolioKpi
{
    public ViewMode ViewMode { get; set; }
    public int PropertyCount { get; set; }

    public decimal TotalInvestment { get; set; }
    public decimal TotalMarketValue { get; set; }
    public decimal TotalRemainingDebt { get; set; }
    public decimal TotalEquity { get; set; }
    public decimal TotalEquityInvested { get; set; }

    public decimal ColdRentMonthly { get; set; }
    public decimal ColdRentAnnual { get; set; }
    public decimal TotalMonthlyDebtService { get; set; }

    public decimal CashflowMonthly { get; set; }
    public decimal CashflowAnnual { get; set; }

    public decimal GrossRentalYieldPercent { get; set; }
    public decimal NetRentalYieldPercent { get; set; }
    public decimal CashOnCashReturnPercent { get; set; }
    public decimal RoiPercent { get; set; }
    public decimal LoanToValuePercent { get; set; }

    public List<PropertyKpi> PropertyKpis { get; set; } = new();
}
