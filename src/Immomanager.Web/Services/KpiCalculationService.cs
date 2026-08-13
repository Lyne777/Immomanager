using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

/// <summary>Berechnet alle Rendite- und Risikokennzahlen für einzelne Immobilien und das Gesamtportfolio.
/// Alle Werte werden je nach <see cref="ViewMode"/> auf Gesamtobjekt- oder Eigenanteils-Basis berechnet.</summary>
public class KpiCalculationService
{
    public PropertyKpi Calculate(Property property, ViewMode viewMode)
    {
        var share = viewMode == ViewMode.MyShare ? property.OwnershipSharePercent / 100m : 1m;

        var purchasePrice = property.PurchasePrice * share;
        var totalAcquisitionCosts = property.TotalAcquisitionCosts * share;
        var totalInvestment = property.TotalInvestment * share;
        var currentMarketValue = property.CurrentMarketValue * share;

        var coldRentMonthly = property.CurrentColdRentMonthly * share;
        var coldRentAnnual = coldRentMonthly * 12;
        var nonAllocableCostsMonthly = property.NonAllocableCostsMonthly * share;
        var nonAllocableCostsAnnual = nonAllocableCostsMonthly * 12;

        var totalRemainingDebt = property.Financings.Sum(f => f.CurrentRemainingDebt) * share;
        var totalMonthlyDebtService = property.Financings.Sum(f => f.MonthlyPayment) * share;
        var principalPaidToDate = property.Financings.Sum(f => f.PrincipalPaidToDate) * share;
        var originalLoanAmount = property.Financings.Sum(f => f.OriginalLoanAmount) * share;

        var equityInvested = totalInvestment - originalLoanAmount;
        var currentEquityValue = currentMarketValue - totalRemainingDebt;

        var cashflowMonthly = coldRentMonthly - nonAllocableCostsMonthly - totalMonthlyDebtService;
        var cashflowAnnual = cashflowMonthly * 12;

        return new PropertyKpi
        {
            PropertyId = property.Id,
            PropertyName = property.Name,
            ViewMode = viewMode,
            SharePercent = property.OwnershipSharePercent,

            PurchasePrice = purchasePrice,
            TotalAcquisitionCosts = totalAcquisitionCosts,
            TotalInvestment = totalInvestment,
            CurrentMarketValue = currentMarketValue,

            ColdRentMonthly = coldRentMonthly,
            ColdRentAnnual = coldRentAnnual,
            NonAllocableCostsMonthly = nonAllocableCostsMonthly,
            NonAllocableCostsAnnual = nonAllocableCostsAnnual,

            TotalRemainingDebt = totalRemainingDebt,
            TotalMonthlyDebtService = totalMonthlyDebtService,
            PrincipalPaidToDate = principalPaidToDate,
            EquityInvested = equityInvested,
            CurrentEquityValue = currentEquityValue,

            CashflowMonthly = cashflowMonthly,
            CashflowAnnual = cashflowAnnual,

            GrossRentalYieldPercent = SafeDivide(coldRentAnnual, purchasePrice) * 100,
            NetRentalYieldPercent = SafeDivide(coldRentAnnual - nonAllocableCostsAnnual, totalInvestment) * 100,
            CashOnCashReturnPercent = SafeDivide(cashflowAnnual, equityInvested) * 100,
            RoiPercent = SafeDivide(currentEquityValue - equityInvested, equityInvested) * 100,
            LoanToValuePercent = SafeDivide(totalRemainingDebt, currentMarketValue) * 100,
        };
    }

    public PortfolioKpi CalculatePortfolio(IEnumerable<Property> properties, ViewMode viewMode)
    {
        var propertyKpis = properties.Select(p => Calculate(p, viewMode)).ToList();

        var totalInvestment = propertyKpis.Sum(k => k.TotalInvestment);
        var totalMarketValue = propertyKpis.Sum(k => k.CurrentMarketValue);
        var totalRemainingDebt = propertyKpis.Sum(k => k.TotalRemainingDebt);
        var totalEquityInvested = propertyKpis.Sum(k => k.EquityInvested);
        var totalPurchasePrice = propertyKpis.Sum(k => k.PurchasePrice);
        var coldRentAnnual = propertyKpis.Sum(k => k.ColdRentAnnual);
        var nonAllocableCostsAnnual = propertyKpis.Sum(k => k.NonAllocableCostsAnnual);
        var cashflowMonthly = propertyKpis.Sum(k => k.CashflowMonthly);
        var totalEquity = totalMarketValue - totalRemainingDebt;

        return new PortfolioKpi
        {
            ViewMode = viewMode,
            PropertyCount = propertyKpis.Count,

            TotalInvestment = totalInvestment,
            TotalMarketValue = totalMarketValue,
            TotalRemainingDebt = totalRemainingDebt,
            TotalEquity = totalEquity,
            TotalEquityInvested = totalEquityInvested,

            ColdRentMonthly = propertyKpis.Sum(k => k.ColdRentMonthly),
            ColdRentAnnual = coldRentAnnual,
            TotalMonthlyDebtService = propertyKpis.Sum(k => k.TotalMonthlyDebtService),

            CashflowMonthly = cashflowMonthly,
            CashflowAnnual = cashflowMonthly * 12,

            GrossRentalYieldPercent = SafeDivide(coldRentAnnual, totalPurchasePrice) * 100,
            NetRentalYieldPercent = SafeDivide(coldRentAnnual - nonAllocableCostsAnnual, totalInvestment) * 100,
            CashOnCashReturnPercent = SafeDivide(cashflowMonthly * 12, totalEquityInvested) * 100,
            RoiPercent = SafeDivide(totalEquity - totalEquityInvested, totalEquityInvested) * 100,
            LoanToValuePercent = SafeDivide(totalRemainingDebt, totalMarketValue) * 100,

            PropertyKpis = propertyKpis,
        };
    }

    private static decimal SafeDivide(decimal numerator, decimal denominator) =>
        denominator == 0 ? 0 : numerator / denominator;
}
