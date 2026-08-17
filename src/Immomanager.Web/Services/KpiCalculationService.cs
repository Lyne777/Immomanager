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

        // Vereinfachtes Ertragswertverfahren: Ertragswert = Jahresnettokaltmiete × Vervielfältiger.
        // Bewusst kein Rückgriff auf das vollständige ImmoWertV-Verfahren (Bodenwert,
        // Bewirtschaftungskosten, Liegenschaftszinssatz, Restnutzungsdauer) - der Nutzer möchte
        // explizit die einfache Multiplikator-Variante, mit dem Multiplikator als einzigem manuell zu
        // pflegenden Stammdatum. Ohne Multiplikator wird bewusst kein Wert geraten (null).
        var estimatedIncomeValue = property.IncomeMultiplier is { } multiplier
            ? coldRentAnnual * multiplier
            : (decimal?)null;

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

            EstimatedIncomeValue = estimatedIncomeValue,
        };
    }

    /// <summary>Rekonstruiert die Kaltmieten-Entwicklung eines Objekts (Summe über alle Einheiten,
    /// anteilig je <see cref="ViewMode"/>) aus den tatsächlichen Mietverhältnissen - jede Einheit
    /// kann über die Zeit mehrere <see cref="Tenancy"/>-Einträge mit eigener, historisch erhaltener
    /// Kaltmiete haben (siehe <see cref="Tenancy.ColdRentMonthly"/>), daraus lässt sich die
    /// tatsächliche Entwicklung ableiten statt sie nur zwischen zwei Punkten zu erraten. Stützpunkte
    /// entstehen bei jedem Mietbeginn und jedem Auszug (Miete kann auf Leerstand = 0 springen).
    /// <see cref="Property.ColdRentMonthlyAtPurchase"/> dient nur noch als Fallback für den
    /// Kaufzeitpunkt, falls zu diesem keinerlei Mietverhältnis bekannt ist (z. B. weil Mietverhältnisse
    /// erst ab einem späteren Datum im System erfasst wurden). Der letzte Punkt ("heute") nutzt bewusst
    /// die aktuellen Einheiten-Kaltmieten (wie überall sonst in der App), nicht zwingend den letzten
    /// Tenancy-Eintrag, falls dieser nicht mehr aktuell gepflegt wurde.</summary>
    public List<RentHistoryPoint> BuildRentHistory(Property property, ViewMode viewMode)
    {
        var share = viewMode == ViewMode.MyShare ? property.OwnershipSharePercent / 100m : 1m;
        var today = DateOnly.FromDateTime(DateTime.Today);

        var changePoints = new SortedSet<DateOnly> { property.PurchaseDate };
        foreach (var tenancy in property.Units.SelectMany(u => u.Tenancies))
        {
            if (tenancy.MoveInDate > property.PurchaseDate && tenancy.MoveInDate <= today)
            {
                changePoints.Add(tenancy.MoveInDate);
            }

            if (tenancy.MoveOutDate is { } moveOutDate)
            {
                var vacancyStart = moveOutDate.AddDays(1);
                if (vacancyStart > property.PurchaseDate && vacancyStart <= today)
                {
                    changePoints.Add(vacancyStart);
                }
            }
        }

        var points = changePoints
            .Select(date => new RentHistoryPoint { Date = date, ColdRentMonthly = TotalRentAt(property, date) * share })
            .ToList();

        if (points.Count > 0 && points[0].Date == property.PurchaseDate && points[0].ColdRentMonthly == 0
            && property.ColdRentMonthlyAtPurchase is { } rentAtPurchase)
        {
            points[0].ColdRentMonthly = rentAtPurchase * share;
        }

        var currentTotal = property.CurrentColdRentMonthly * share;
        if (points.Count > 0 && points[^1].Date == today)
        {
            points[^1].ColdRentMonthly = currentTotal;
        }
        else
        {
            points.Add(new RentHistoryPoint { Date = today, ColdRentMonthly = currentTotal });
        }

        return points;
    }

    /// <summary>Summe der zum angegebenen Datum aktiven Mietverhältnisse über alle Einheiten - Einheiten
    /// ohne zu diesem Zeitpunkt bekanntes Mietverhältnis zählen als Leerstand (0), nicht als "unbekannt".</summary>
    private static decimal TotalRentAt(Property property, DateOnly date) =>
        property.Units.Sum(unit => unit.Tenancies
            .Where(t => t.MoveInDate <= date && (!t.MoveOutDate.HasValue || t.MoveOutDate.Value >= date))
            .OrderByDescending(t => t.MoveInDate)
            .FirstOrDefault()?.ColdRentMonthly ?? 0);

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
