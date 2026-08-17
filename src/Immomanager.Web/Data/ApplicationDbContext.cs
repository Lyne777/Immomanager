using Immomanager.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Immomanager.Web.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Financing> Financings => Set<Financing>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<RenovationProject> RenovationProjects => Set<RenovationProject>();
    public DbSet<RenovationLineItem> RenovationLineItems => Set<RenovationLineItem>();
    public DbSet<DealCalculation> DealCalculations => Set<DealCalculation>();
    public DbSet<UnitCalculation> UnitCalculations => Set<UnitCalculation>();
    public DbSet<LoanCalculation> LoanCalculations => Set<LoanCalculation>();
    public DbSet<CalculationScenario> CalculationScenarios => Set<CalculationScenario>();
    public DbSet<RentTarget> RentTargets => Set<RentTarget>();
    public DbSet<PropertyUnit> PropertyUnits => Set<PropertyUnit>();
    public DbSet<InsurancePolicy> InsurancePolicies => Set<InsurancePolicy>();
    public DbSet<InsuranceCheckItem> InsuranceCheckItems => Set<InsuranceCheckItem>();
    public DbSet<UtilityStatement> UtilityStatements => Set<UtilityStatement>();
    public DbSet<UtilityCostItem> UtilityCostItems => Set<UtilityCostItem>();
    public DbSet<UtilityStatementDocument> UtilityStatementDocuments => Set<UtilityStatementDocument>();
    public DbSet<Tenancy> Tenancies => Set<Tenancy>();
    public DbSet<PropertyDocument> PropertyDocuments => Set<PropertyDocument>();
    public DbSet<UnitDocument> UnitDocuments => Set<UnitDocument>();
    public DbSet<RepaymentVehicle> RepaymentVehicles => Set<RepaymentVehicle>();
    public DbSet<PropertyLogEntry> PropertyLogEntries => Set<PropertyLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Property>(entity =>
        {
            entity.Property(p => p.PurchasePrice).HasPrecision(18, 2);
            entity.Property(p => p.PropertyTransferTax).HasPrecision(18, 2);
            entity.Property(p => p.NotaryAndRegistryCosts).HasPrecision(18, 2);
            entity.Property(p => p.BrokerCommission).HasPrecision(18, 2);
            entity.Property(p => p.InitialRenovationCosts).HasPrecision(18, 2);
            entity.Property(p => p.OwnershipSharePercent).HasPrecision(5, 2);
            entity.Property(p => p.CurrentMarketValue).HasPrecision(18, 2);
            entity.Property(p => p.IncomeMultiplier).HasPrecision(5, 2);
            entity.Property(p => p.ColdRentMonthlyAtPurchase).HasPrecision(18, 2);

            // LivingAreaSqm/CurrentColdRentMonthly/NonAllocableCostsMonthly sind berechnete
            // C#-Properties (Summe aus Units) ohne Setter - EF Core mappt sie per Konvention
            // automatisch nicht als Spalten, daher hier bewusst keine HasPrecision-Konfiguration.

            entity.HasMany(p => p.Financings)
                .WithOne(f => f.Property)
                .HasForeignKey(f => f.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(p => p.Images)
                .WithOne(i => i.Property)
                .HasForeignKey(i => i.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(p => p.RenovationProjects)
                .WithOne(r => r.Property)
                .HasForeignKey(r => r.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(p => p.RentTargets)
                .WithOne(t => t.Property)
                .HasForeignKey(t => t.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(p => p.Units)
                .WithOne(u => u.Property)
                .HasForeignKey(u => u.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(p => p.InsurancePolicies)
                .WithOne(i => i.Property)
                .HasForeignKey(i => i.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(p => p.InsuranceCheckItems)
                .WithOne(c => c.Property)
                .HasForeignKey(c => c.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(p => p.UtilityStatements)
                .WithOne(s => s.Property)
                .HasForeignKey(s => s.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(p => p.Documents)
                .WithOne(d => d.Property)
                .HasForeignKey(d => d.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(p => p.LogEntries)
                .WithOne(e => e.Property)
                .HasForeignKey(e => e.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PropertyLogEntry>(entity =>
        {
            // SetNull statt Cascade: Löschen einer Einheit soll die historische Notiz nicht mitreißen,
            // sie bezieht sich dann eben nur noch aufs ganze Objekt statt auf die (nicht mehr
            // existierende) Einheit.
            entity.HasOne(e => e.PropertyUnit)
                .WithMany()
                .HasForeignKey(e => e.PropertyUnitId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<UtilityStatement>(entity =>
        {
            entity.Property(s => s.TotalCosts).HasPrecision(18, 2);
            entity.Property(s => s.PeriodMonths).HasPrecision(5, 2);

            // Nur eine Abrechnung je Einheit und Abrechnungsjahr (NULLs - je Objekt - werden von
            // SQLite als paarweise verschieden behandelt, blockieren sich also nicht gegenseitig;
            // dafür sorgt der gefilterte Index direkt darunter).
            entity.HasIndex(s => new { s.PropertyUnitId, s.Year }).IsUnique();

            // Nur eine gemeinsame Ganzes-Objekt-Abrechnung (PropertyUnitId = null) je Immobilie und Jahr.
            entity.HasIndex(s => new { s.PropertyId, s.Year })
                .IsUnique()
                .HasFilter("\"PropertyUnitId\" IS NULL");

            entity.HasOne(s => s.PropertyUnit)
                .WithMany()
                .HasForeignKey(s => s.PropertyUnitId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(s => s.Items)
                .WithOne(i => i.UtilityStatement)
                .HasForeignKey(i => i.UtilityStatementId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(s => s.Documents)
                .WithOne(d => d.UtilityStatement)
                .HasForeignKey(d => d.UtilityStatementId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UtilityCostItem>(entity =>
        {
            entity.Property(i => i.Amount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<PropertyUnit>(entity =>
        {
            entity.Property(u => u.AreaSqm).HasPrecision(10, 2);
            entity.Property(u => u.ColdRentMonthly).HasPrecision(18, 2);
            entity.Property(u => u.NonAllocableCostsMonthly).HasPrecision(18, 2);
            // Default true, damit bestehende Einheiten beim Schema-Umbau ihr bisheriges Verhalten
            // (Fläche/Miete zählt zur Soll-Miete-Berechnung) unverändert behalten.
            entity.Property(u => u.CountsTowardRentTarget).HasDefaultValue(true);

            entity.HasMany(u => u.Tenancies)
                .WithOne(t => t.PropertyUnit)
                .HasForeignKey(t => t.PropertyUnitId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(u => u.Documents)
                .WithOne(d => d.PropertyUnit)
                .HasForeignKey(d => d.PropertyUnitId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Tenancy>(entity =>
        {
            entity.Property(t => t.ColdRentMonthly).HasPrecision(18, 2);
            entity.Property(t => t.AdvancePaymentMonthly).HasPrecision(18, 2);
            entity.Property(t => t.SecurityDeposit).HasPrecision(18, 2);
        });

        modelBuilder.Entity<InsurancePolicy>(entity =>
        {
            entity.Property(i => i.AnnualPremium).HasPrecision(18, 2);

            // Nur eine aktuelle Police je Immobilie und Kategorie.
            entity.HasIndex(i => new { i.PropertyId, i.Category }).IsUnique();
        });

        modelBuilder.Entity<InsuranceCheckItem>(entity =>
        {
            // Ein Katalog-Eintrag ("Key") kommt je Immobilie nur einmal vor - verhindert doppeltes
            // Seeding und macht das Nachziehen fehlender Positionen (neue Katalog-Einträge) idempotent.
            entity.HasIndex(c => new { c.PropertyId, c.Key }).IsUnique();
        });

        modelBuilder.Entity<Financing>(entity =>
        {
            entity.Property(f => f.OriginalLoanAmount).HasPrecision(18, 2);
            entity.Property(f => f.CurrentRemainingDebt).HasPrecision(18, 2);
            entity.Property(f => f.InterestRatePercent).HasPrecision(5, 3);
            entity.Property(f => f.InitialRepaymentRatePercent).HasPrecision(5, 3);
            entity.Property(f => f.MonthlyPayment).HasPrecision(18, 2);

            entity.HasMany(f => f.RepaymentVehicles)
                .WithOne(r => r.Financing)
                .HasForeignKey(r => r.FinancingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RepaymentVehicle>(entity =>
        {
            entity.Property(r => r.TargetAmount).HasPrecision(18, 2);
            entity.Property(r => r.MonthlyContribution).HasPrecision(18, 2);
            entity.Property(r => r.CurrentValue).HasPrecision(18, 2);
        });

        modelBuilder.Entity<RenovationProject>(entity =>
        {
            entity.Property(r => r.AreaSqm).HasPrecision(10, 2);
            entity.Property(r => r.PlannedTotalCost).HasPrecision(18, 2);

            entity.HasMany(r => r.LineItems)
                .WithOne(li => li.RenovationProject)
                .HasForeignKey(li => li.RenovationProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RenovationLineItem>(entity =>
        {
            entity.Property(li => li.Quantity).HasPrecision(10, 2);
            entity.Property(li => li.MaterialCost).HasPrecision(18, 2);
            entity.Property(li => li.LaborCost).HasPrecision(18, 2);
            entity.Property(li => li.SelfLaborHours).HasPrecision(8, 2);
        });

        modelBuilder.Entity<DealCalculation>(entity =>
        {
            entity.Property(d => d.PurchasePrice).HasPrecision(18, 2);
            entity.Property(d => d.LivingAreaSqm).HasPrecision(10, 2);
            entity.Property(d => d.BrokerFeePercent).HasPrecision(5, 2);
            entity.Property(d => d.NotaryFeePercent).HasPrecision(5, 2);
            entity.Property(d => d.LandRegistryFeePercent).HasPrecision(5, 2);
            entity.Property(d => d.RealEstateTransferTaxPercent).HasPrecision(5, 2);
            entity.Property(d => d.OtherAcquisitionCosts).HasPrecision(18, 2);
            entity.Property(d => d.InitialRenovationCost).HasPrecision(18, 2);
            entity.Property(d => d.GlobalMonthlyNetColdRent).HasPrecision(18, 2);
            entity.Property(d => d.ParkingIncomeMonthly).HasPrecision(18, 2);
            entity.Property(d => d.OtherIncomeMonthly).HasPrecision(18, 2);
            entity.Property(d => d.RentIncreasePercentPa).HasPrecision(5, 2);
            entity.Property(d => d.NonAllocableCostsMonthly).HasPrecision(18, 2);
            entity.Property(d => d.MaintenanceReservePerSqmPa).HasPrecision(10, 2);
            entity.Property(d => d.VacancyRiskPercent).HasPrecision(5, 2);
            entity.Property(d => d.CostInflationPercentPa).HasPrecision(5, 2);
            entity.Property(d => d.BuildingSharePercent).HasPrecision(5, 2);
            entity.Property(d => d.AfaRatePercent).HasPrecision(5, 2);
            entity.Property(d => d.PersonalMarginalTaxRatePercent).HasPrecision(5, 2);
            entity.Property(d => d.AnnualValueAppreciationPercent).HasPrecision(5, 2);

            entity.HasOne(d => d.Property)
                .WithMany()
                .HasForeignKey(d => d.PropertyId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(d => d.Units)
                .WithOne(u => u.DealCalculation)
                .HasForeignKey(u => u.DealCalculationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(d => d.Loans)
                .WithOne(l => l.DealCalculation)
                .HasForeignKey(l => l.DealCalculationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(d => d.Scenarios)
                .WithOne(s => s.DealCalculation)
                .HasForeignKey(s => s.DealCalculationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UnitCalculation>(entity =>
        {
            entity.Property(u => u.AreaSqm).HasPrecision(10, 2);
            entity.Property(u => u.CurrentRentMonthly).HasPrecision(18, 2);
            entity.Property(u => u.TargetRentMonthly).HasPrecision(18, 2);
        });

        modelBuilder.Entity<LoanCalculation>(entity =>
        {
            entity.Property(l => l.LoanAmount).HasPrecision(18, 2);
            entity.Property(l => l.InterestRatePercent).HasPrecision(5, 3);
            entity.Property(l => l.InitialRepaymentRatePercent).HasPrecision(5, 3);
            entity.Property(l => l.AnnualSpecialRepayment).HasPrecision(18, 2);
        });

        modelBuilder.Entity<CalculationScenario>(entity =>
        {
            entity.Property(s => s.PurchasePriceDeltaPercent).HasPrecision(5, 2);
            entity.Property(s => s.InterestRateDeltaPercentPoints).HasPrecision(5, 3);
            entity.Property(s => s.RentDeltaPercent).HasPrecision(5, 2);
        });

        modelBuilder.Entity<RentTarget>(entity =>
        {
            entity.Property(t => t.TargetRentPerSqm).HasPrecision(10, 2);

            // Nur ein Soll-Wert je Immobilie und Quartal.
            entity.HasIndex(t => new { t.PropertyId, t.Year, t.Quarter }).IsUnique();
        });
    }
}
