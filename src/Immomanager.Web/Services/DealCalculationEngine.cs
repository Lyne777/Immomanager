using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

/// <summary>Reine Rechen-Engine für Ankaufsprüfungen (kein Datenbankzugriff): Kaufnebenkosten, die
/// 15%-Grenze für anschaffungsnahe Herstellungskosten, AfA/Denkmal-AfA, Live-Kennzahlen sowie die
/// jahrgenaue 30-50-Jahres-Prognose inkl. Zinsänderungsrisiko und Szenario-Simulation.</summary>
public class DealCalculationEngine
{
    private record AcquisitionBreakdown(decimal TotalAcquisitionCosts, decimal TotalInvestment, decimal BuildingValue);

    public DealCalculationResult Calculate(DealCalculation deal)
    {
        var acquisition = CalculateAcquisition(deal);
        var renovationCheck = CheckRenovationThreshold(deal, acquisition.BuildingValue);
        var totalLoanAmount = deal.Loans.Sum(l => l.LoanAmount);
        var equityRequired = acquisition.TotalInvestment - totalLoanAmount;

        var projection = BuildProjection(deal, acquisition, renovationCheck, equityRequired);
        var year1 = projection.FirstOrDefault();

        var result = new DealCalculationResult
        {
            TotalAcquisitionCosts = acquisition.TotalAcquisitionCosts,
            TotalInvestment = acquisition.TotalInvestment,
            TotalLoanAmount = totalLoanAmount,
            EquityRequired = equityRequired,
            RenovationCheck = renovationCheck,
            PricePerSqm = deal.LivingAreaSqm > 0 ? deal.PurchasePrice / deal.LivingAreaSqm : 0,
            PriceMultiplier = year1 is { GrossRentAnnual: > 0 } ? deal.PurchasePrice / year1.GrossRentAnnual : 0,
            GrossRentalYieldPercent = year1 is not null && deal.PurchasePrice > 0 ? year1.GrossRentAnnual / deal.PurchasePrice * 100 : 0,
            NetRentalYieldPercent = year1 is not null && acquisition.TotalInvestment > 0
                ? (year1.GrossRentAnnual - year1.OperatingCostsAnnual) / acquisition.TotalInvestment * 100
                : 0,
            CashflowBeforeTaxMonthly = year1 is not null ? year1.CashflowBeforeTax / 12 : 0,
            CashflowAfterTaxMonthly = year1 is not null ? year1.CashflowAfterTax / 12 : 0,
            EquityReturnPercent = year1 is not null && equityRequired > 0 ? year1.CashflowAfterTax / equityRequired * 100 : 0,
            Projection = projection,
        };

        result.BreakEvenCashflowYear = projection.FirstOrDefault(p => p.CashflowAfterTax > 0)?.Year;
        result.BreakEvenTotalYear = projection.FirstOrDefault(p => p.CumulativeCashflowAfterTax > 0)?.Year;
        result.FullyRepaidYear = deal.Loans.Count > 0 ? projection.FirstOrDefault(p => p.RemainingDebt <= 0.01m)?.Year : null;
        result.MaxSustainableInterestRateIncreasePoints = FindMaxSustainableRateDelta(deal);

        return result;
    }

    /// <summary>Erstellt eine In-Memory-Kopie der Kalkulation mit angepasstem Kaufpreis, Zinssatz (je
    /// Darlehen um dieselbe Anzahl Prozentpunkte verschoben) und Miete - Basis für Sensitivitäts- und
    /// Szenarioanalysen, ohne die Originalkalkulation zu verändern oder in der DB zu speichern.</summary>
    public DealCalculation CloneForScenario(DealCalculation deal, decimal purchasePriceDeltaPercent, decimal interestRateDeltaPoints, decimal rentDeltaPercent)
    {
        var priceFactor = 1 + purchasePriceDeltaPercent / 100;
        var rentFactor = 1 + rentDeltaPercent / 100;

        return new DealCalculation
        {
            Id = deal.Id,
            Name = deal.Name,
            PurchasePrice = deal.PurchasePrice * priceFactor,
            LivingAreaSqm = deal.LivingAreaSqm,
            YearBuilt = deal.YearBuilt,
            PurchaseDate = deal.PurchaseDate,
            ParkingSpaces = deal.ParkingSpaces,
            BrokerFeePercent = deal.BrokerFeePercent,
            NotaryFeePercent = deal.NotaryFeePercent,
            LandRegistryFeePercent = deal.LandRegistryFeePercent,
            RealEstateTransferTaxPercent = deal.RealEstateTransferTaxPercent,
            OtherAcquisitionCosts = deal.OtherAcquisitionCosts,
            InitialRenovationCost = deal.InitialRenovationCost,
            RentalIncomeMode = deal.RentalIncomeMode,
            GlobalMonthlyNetColdRent = deal.GlobalMonthlyNetColdRent * rentFactor,
            ParkingIncomeMonthly = deal.ParkingIncomeMonthly * rentFactor,
            OtherIncomeMonthly = deal.OtherIncomeMonthly * rentFactor,
            RentIncreasePercentPa = deal.RentIncreasePercentPa,
            NonAllocableCostsMonthly = deal.NonAllocableCostsMonthly,
            MaintenanceReservePerSqmPa = deal.MaintenanceReservePerSqmPa,
            VacancyRiskPercent = deal.VacancyRiskPercent,
            CostInflationPercentPa = deal.CostInflationPercentPa,
            BuildingSharePercent = deal.BuildingSharePercent,
            AfaRatePercent = deal.AfaRatePercent,
            UseMonumentAfa = deal.UseMonumentAfa,
            PersonalMarginalTaxRatePercent = deal.PersonalMarginalTaxRatePercent,
            AnnualValueAppreciationPercent = deal.AnnualValueAppreciationPercent,
            ProjectionYears = deal.ProjectionYears,
            Units = deal.Units.Select(u => new UnitCalculation
            {
                UnitLabel = u.UnitLabel,
                AreaSqm = u.AreaSqm,
                CurrentRentMonthly = u.CurrentRentMonthly * rentFactor,
                TargetRentMonthly = u.TargetRentMonthly * rentFactor,
                TargetRentReachedInYear = u.TargetRentReachedInYear,
            }).ToList(),
            // Die Darlehenssumme bleibt bei einer Kaufpreis-Sensitivität bewusst unverändert (das Eigenkapital
            // federt die Differenz ab) - nur der Zins wird um das Delta verschoben.
            Loans = deal.Loans.Select(l => new LoanCalculation
            {
                Name = l.Name,
                LoanAmount = l.LoanAmount,
                InterestRatePercent = Math.Max(0, l.InterestRatePercent + interestRateDeltaPoints),
                InitialRepaymentRatePercent = l.InitialRepaymentRatePercent,
                AnnualSpecialRepayment = l.AnnualSpecialRepayment,
                FixedInterestYears = l.FixedInterestYears,
            }).ToList(),
        };
    }

    private static AcquisitionBreakdown CalculateAcquisition(DealCalculation deal)
    {
        var brokerFee = deal.PurchasePrice * deal.BrokerFeePercent / 100;
        var notaryFee = deal.PurchasePrice * deal.NotaryFeePercent / 100;
        var landRegistryFee = deal.PurchasePrice * deal.LandRegistryFeePercent / 100;
        var transferTax = deal.PurchasePrice * deal.RealEstateTransferTaxPercent / 100;
        var totalAcquisitionCosts = brokerFee + notaryFee + landRegistryFee + transferTax + deal.OtherAcquisitionCosts;
        var totalInvestment = deal.PurchasePrice + totalAcquisitionCosts + deal.InitialRenovationCost;
        var buildingValue = deal.PurchasePrice * deal.BuildingSharePercent / 100;

        return new AcquisitionBreakdown(totalAcquisitionCosts, totalInvestment, buildingValue);
    }

    private static RenovationTaxCheck CheckRenovationThreshold(DealCalculation deal, decimal buildingValue)
    {
        var threshold = buildingValue * 0.15m;
        return new RenovationTaxCheck
        {
            BuildingValue = buildingValue,
            ThresholdAmount = threshold,
            RenovationCost = deal.InitialRenovationCost,
        };
    }

    private static List<YearlyProjectionRow> BuildProjection(
        DealCalculation deal, AcquisitionBreakdown acquisition, RenovationTaxCheck renovationCheck, decimal equityRequired)
    {
        var years = Math.Clamp(deal.ProjectionYears, 1, 50);
        var loanSchedules = deal.Loans.Select(l => LoanAmortizationCalculator.BuildSchedule(l, years)).ToList();

        // Wird die Sanierung nicht sofort abgezogen (Denkmal-AfA oder über der 15%-Grenze), fließt sie
        // stattdessen in die reguläre AfA-Bemessungsgrundlage ein.
        var capitalizeIntoRegularAfa = renovationCheck.IsExceeded && !deal.UseMonumentAfa;
        var regularAfaBasis = acquisition.BuildingValue + (capitalizeIntoRegularAfa ? deal.InitialRenovationCost : 0);
        var regularAfaAnnual = regularAfaBasis * deal.AfaRatePercent / 100;
        var remainingRegularAfaBasis = regularAfaBasis;

        var immediateDeductibleRenovation = !renovationCheck.IsExceeded && !deal.UseMonumentAfa ? deal.InitialRenovationCost : 0;

        var rows = new List<YearlyProjectionRow>();
        var cumulativeCashflow = -equityRequired;

        for (var year = 1; year <= years; year++)
        {
            var rentGrowthFactor = (double)(1 + deal.RentIncreasePercentPa / 100);

            decimal baseRentMonthly;
            if (deal.RentalIncomeMode == RentalIncomeMode.UnitBased && deal.Units.Count > 0)
            {
                baseRentMonthly = deal.Units.Sum(u => RentForUnitAtYear(u, year, rentGrowthFactor));
            }
            else
            {
                baseRentMonthly = deal.GlobalMonthlyNetColdRent * (decimal)Math.Pow(rentGrowthFactor, year - 1);
            }

            var parkingAndOtherMonthly = (deal.ParkingIncomeMonthly + deal.OtherIncomeMonthly) * (decimal)Math.Pow(rentGrowthFactor, year - 1);
            var grossRentAnnual = (baseRentMonthly + parkingAndOtherMonthly) * 12;

            var costGrowthFactor = (double)(1 + deal.CostInflationPercentPa / 100);
            var nonAllocableAnnual = deal.NonAllocableCostsMonthly * 12 * (decimal)Math.Pow(costGrowthFactor, year - 1);
            var maintenanceAnnual = deal.MaintenanceReservePerSqmPa * deal.LivingAreaSqm * (decimal)Math.Pow(costGrowthFactor, year - 1);
            var vacancyAnnual = grossRentAnnual * deal.VacancyRiskPercent / 100;
            var operatingCostsAnnual = nonAllocableAnnual + maintenanceAnnual + vacancyAnnual;
            var deductibleOperatingCosts = nonAllocableAnnual;

            var yearInterest = loanSchedules.Sum(s => s[year - 1].Interest);
            var yearPrincipal = loanSchedules.Sum(s => s[year - 1].Principal);
            var remainingDebt = loanSchedules.Sum(s => s[year - 1].EndBalance);

            var regularAfaThisYear = Math.Min(regularAfaAnnual, remainingRegularAfaBasis);
            remainingRegularAfaBasis -= regularAfaThisYear;
            var monumentAfaThisYear = deal.UseMonumentAfa
                ? year <= 8 ? deal.InitialRenovationCost * 0.09m
                : year <= 12 ? deal.InitialRenovationCost * 0.07m
                : 0
                : 0;
            var afaAmount = regularAfaThisYear + monumentAfaThisYear;

            var yearImmediateDeductibleRenovation = year == 1 ? immediateDeductibleRenovation : 0;

            var taxableIncome = grossRentAnnual - deductibleOperatingCosts - yearInterest - afaAmount - yearImmediateDeductibleRenovation;
            var taxAmount = taxableIncome * deal.PersonalMarginalTaxRatePercent / 100;

            var cashflowBeforeTax = grossRentAnnual - operatingCostsAnnual - yearInterest - yearPrincipal;
            var cashflowAfterTax = cashflowBeforeTax - taxAmount;
            cumulativeCashflow += cashflowAfterTax;

            var propertyValue = deal.PurchasePrice * (decimal)Math.Pow((double)(1 + deal.AnnualValueAppreciationPercent / 100), year);

            rows.Add(new YearlyProjectionRow
            {
                Year = year,
                GrossRentAnnual = grossRentAnnual,
                OperatingCostsAnnual = operatingCostsAnnual,
                DeductibleOperatingCosts = deductibleOperatingCosts,
                InterestPaid = yearInterest,
                PrincipalPaid = yearPrincipal,
                RemainingDebt = remainingDebt,
                AfaAmount = afaAmount,
                ImmediateDeductibleRenovation = yearImmediateDeductibleRenovation,
                TaxableIncome = taxableIncome,
                TaxAmount = taxAmount,
                CashflowBeforeTax = cashflowBeforeTax,
                CashflowAfterTax = cashflowAfterTax,
                CumulativeCashflowAfterTax = cumulativeCashflow,
                PropertyValue = propertyValue,
                NetWorth = propertyValue - remainingDebt,
            });
        }

        return rows;
    }

    private static decimal RentForUnitAtYear(UnitCalculation unit, int year, double rentGrowthFactor)
    {
        if (year < unit.TargetRentReachedInYear)
        {
            return unit.CurrentRentMonthly;
        }

        if (year == unit.TargetRentReachedInYear)
        {
            return unit.TargetRentMonthly;
        }

        var yearsBeyondTarget = year - unit.TargetRentReachedInYear;
        return unit.TargetRentMonthly * (decimal)Math.Pow(rentGrowthFactor, yearsBeyondTarget);
    }

    /// <summary>Ermittelt per Bisektion, um wie viele Prozentpunkte der Zins auf allen Darlehen maximal
    /// steigen dürfte, bevor der Cashflow nach Steuern in Jahr 1 negativ wird.</summary>
    private decimal? FindMaxSustainableRateDelta(DealCalculation deal)
    {
        if (deal.Loans.Count == 0)
        {
            return null;
        }

        decimal CashflowAtDelta(decimal delta)
        {
            var clone = CloneForScenario(deal, 0, delta, 0);
            var acquisition = CalculateAcquisition(clone);
            var renovationCheck = CheckRenovationThreshold(clone, acquisition.BuildingValue);
            var equity = acquisition.TotalInvestment - clone.Loans.Sum(l => l.LoanAmount);
            var projection = BuildProjection(clone, acquisition, renovationCheck, equity);
            return projection[0].CashflowAfterTax;
        }

        if (CashflowAtDelta(0) <= 0)
        {
            return 0;
        }

        const decimal searchCeiling = 20m;
        if (CashflowAtDelta(searchCeiling) > 0)
        {
            return null;
        }

        var low = 0m;
        var high = searchCeiling;
        for (var i = 0; i < 40; i++)
        {
            var mid = (low + high) / 2;
            if (CashflowAtDelta(mid) > 0)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }
}
