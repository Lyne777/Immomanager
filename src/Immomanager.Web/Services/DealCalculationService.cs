using Immomanager.Web.Data;
using Immomanager.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Immomanager.Web.Services;

public class DealCalculationService : IDealCalculationService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public DealCalculationService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<DealCalculation>> GetAllAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.DealCalculations
            .Include(d => d.Property)
            .Include(d => d.Units)
            .Include(d => d.Loans)
            .AsNoTracking()
            .OrderByDescending(d => d.UpdatedAtUtc)
            .ToListAsync();
    }

    public async Task<DealCalculation?> GetByIdAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.DealCalculations
            .Include(d => d.Property)
            .Include(d => d.Units)
            .Include(d => d.Loans)
            .Include(d => d.Scenarios)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<DealCalculation> CreateAsync(DealCalculation deal)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        deal.CreatedAtUtc = DateTime.UtcNow;
        deal.UpdatedAtUtc = DateTime.UtcNow;
        db.DealCalculations.Add(deal);
        await db.SaveChangesAsync();
        return deal;
    }

    public async Task UpdateAsync(DealCalculation deal)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // Kind-Sammlungen (Einheiten/Darlehen/Szenarien) werden komplett ersetzt statt einzeln
        // abgeglichen - bei den hier üblichen kleinen Listen einfacher und robuster als ein
        // Graph-Diff, und vermeidet Tracking-Konflikte mit dem übergebenen, losgelösten Objekt.
        await db.UnitCalculations.Where(u => u.DealCalculationId == deal.Id).ExecuteDeleteAsync();
        await db.LoanCalculations.Where(l => l.DealCalculationId == deal.Id).ExecuteDeleteAsync();
        await db.CalculationScenarios.Where(s => s.DealCalculationId == deal.Id).ExecuteDeleteAsync();

        foreach (var unit in deal.Units)
        {
            unit.Id = 0;
            unit.DealCalculationId = deal.Id;
        }

        foreach (var loan in deal.Loans)
        {
            loan.Id = 0;
            loan.DealCalculationId = deal.Id;
        }

        foreach (var scenario in deal.Scenarios)
        {
            scenario.Id = 0;
            scenario.DealCalculationId = deal.Id;
        }

        deal.UpdatedAtUtc = DateTime.UtcNow;
        db.DealCalculations.Update(deal);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var deal = await db.DealCalculations.FindAsync(id);
        if (deal is not null)
        {
            db.DealCalculations.Remove(deal);
            await db.SaveChangesAsync();
        }
    }

    public async Task<DealCalculation> DuplicateAsync(int id)
    {
        var source = await GetByIdAsync(id) ?? throw new InvalidOperationException("Kalkulation wurde nicht gefunden.");

        var copy = new DealCalculation
        {
            Name = source.Name + " (Kopie)",
            VersionGroupId = source.VersionGroupId,
            Version = source.Version + 1,
            PropertyId = source.PropertyId,
            PurchasePrice = source.PurchasePrice,
            LivingAreaSqm = source.LivingAreaSqm,
            YearBuilt = source.YearBuilt,
            PurchaseDate = source.PurchaseDate,
            ParkingSpaces = source.ParkingSpaces,
            BrokerFeePercent = source.BrokerFeePercent,
            NotaryFeePercent = source.NotaryFeePercent,
            LandRegistryFeePercent = source.LandRegistryFeePercent,
            RealEstateTransferTaxPercent = source.RealEstateTransferTaxPercent,
            OtherAcquisitionCosts = source.OtherAcquisitionCosts,
            InitialRenovationCost = source.InitialRenovationCost,
            RentalIncomeMode = source.RentalIncomeMode,
            GlobalMonthlyNetColdRent = source.GlobalMonthlyNetColdRent,
            ParkingIncomeMonthly = source.ParkingIncomeMonthly,
            OtherIncomeMonthly = source.OtherIncomeMonthly,
            RentIncreasePercentPa = source.RentIncreasePercentPa,
            NonAllocableCostsMonthly = source.NonAllocableCostsMonthly,
            MaintenanceReservePerSqmPa = source.MaintenanceReservePerSqmPa,
            VacancyRiskPercent = source.VacancyRiskPercent,
            CostInflationPercentPa = source.CostInflationPercentPa,
            BuildingSharePercent = source.BuildingSharePercent,
            AfaRatePercent = source.AfaRatePercent,
            UseMonumentAfa = source.UseMonumentAfa,
            PersonalMarginalTaxRatePercent = source.PersonalMarginalTaxRatePercent,
            AnnualValueAppreciationPercent = source.AnnualValueAppreciationPercent,
            ProjectionYears = source.ProjectionYears,
            Notes = source.Notes,
            Units = source.Units.Select(u => new UnitCalculation
            {
                UnitLabel = u.UnitLabel,
                AreaSqm = u.AreaSqm,
                CurrentRentMonthly = u.CurrentRentMonthly,
                TargetRentMonthly = u.TargetRentMonthly,
                TargetRentReachedInYear = u.TargetRentReachedInYear,
            }).ToList(),
            Loans = source.Loans.Select(l => new LoanCalculation
            {
                Name = l.Name,
                LoanAmount = l.LoanAmount,
                InterestRatePercent = l.InterestRatePercent,
                InitialRepaymentRatePercent = l.InitialRepaymentRatePercent,
                AnnualSpecialRepayment = l.AnnualSpecialRepayment,
                FixedInterestYears = l.FixedInterestYears,
            }).ToList(),
            Scenarios = source.Scenarios.Select(s => new CalculationScenario
            {
                Name = s.Name,
                PurchasePriceDeltaPercent = s.PurchasePriceDeltaPercent,
                InterestRateDeltaPercentPoints = s.InterestRateDeltaPercentPoints,
                RentDeltaPercent = s.RentDeltaPercent,
            }).ToList(),
        };

        return await CreateAsync(copy);
    }
}
