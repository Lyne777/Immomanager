using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Eine Ankaufsprüfung/Deal-Kalkulation - entweder eine freie "Sandbox"-Rechnung vor dem Kauf
/// oder eine gespeicherte historische Kalkulation. Beide teilen sich dasselbe Modell; "Sandbox" ist
/// lediglich der Zustand vor dem ersten Speichern. Optional mit einer echten Immobilie im Portfolio
/// verknüpfbar (<see cref="PropertyId"/>), um Plan- mit Ist-Werten abzugleichen.</summary>
public class DealCalculation
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = "Neue Ankaufsprüfung";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Gruppiert alle Versionen derselben Kalkulation (siehe "Duplizieren").</summary>
    public Guid VersionGroupId { get; set; } = Guid.NewGuid();
    public int Version { get; set; } = 1;

    /// <summary>Optionale Verknüpfung zu einer realen Immobilie im Portfolio für den Plan/Ist-Abgleich.</summary>
    public int? PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property? Property { get; set; }

    // --- Objekt & Kaufpreis ---
    [Range(0, double.MaxValue)]
    public decimal PurchasePrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal LivingAreaSqm { get; set; }

    public int? YearBuilt { get; set; }

    public DateOnly PurchaseDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public int ParkingSpaces { get; set; }

    // --- Kaufnebenkosten (%) ---
    [Range(0, 100)]
    public decimal BrokerFeePercent { get; set; } = 3.57m;

    [Range(0, 100)]
    public decimal NotaryFeePercent { get; set; } = 1.5m;

    [Range(0, 100)]
    public decimal LandRegistryFeePercent { get; set; } = 0.5m;

    [Range(0, 100)]
    public decimal RealEstateTransferTaxPercent { get; set; } = 5m;

    [Range(0, double.MaxValue)]
    public decimal OtherAcquisitionCosts { get; set; }

    // --- Anfängliche Sanierung ---
    [Range(0, double.MaxValue)]
    public decimal InitialRenovationCost { get; set; }

    // --- Mieteinkünfte ---
    public RentalIncomeMode RentalIncomeMode { get; set; } = RentalIncomeMode.Global;

    [Range(0, double.MaxValue)]
    public decimal GlobalMonthlyNetColdRent { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ParkingIncomeMonthly { get; set; }

    [Range(0, double.MaxValue)]
    public decimal OtherIncomeMonthly { get; set; }

    [Range(-20, 20)]
    public decimal RentIncreasePercentPa { get; set; } = 1.5m;

    // --- Bewirtschaftungskosten & Rücklagen ---
    [Range(0, double.MaxValue)]
    public decimal NonAllocableCostsMonthly { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MaintenanceReservePerSqmPa { get; set; } = 10m;

    [Range(0, 100)]
    public decimal VacancyRiskPercent { get; set; } = 2m;

    [Range(-20, 20)]
    public decimal CostInflationPercentPa { get; set; } = 2m;

    // --- Steuern & AfA ---
    [Range(0, 100)]
    public decimal BuildingSharePercent { get; set; } = 70m;

    [Range(0, 20)]
    public decimal AfaRatePercent { get; set; } = 2m;

    /// <summary>Denkmal-AfA nach § 7i EStG: die Sanierungskosten werden statt regulärer AfA/Sofortabzug
    /// über 8 Jahre mit 9 % p.a. und weitere 4 Jahre mit 7 % p.a. abgeschrieben.</summary>
    public bool UseMonumentAfa { get; set; }

    [Range(0, 60)]
    public decimal PersonalMarginalTaxRatePercent { get; set; } = 42m;

    // --- Prognose-Annahmen ---
    [Range(-10, 20)]
    public decimal AnnualValueAppreciationPercent { get; set; } = 1.5m;

    [Range(1, 50)]
    public int ProjectionYears { get; set; } = 30;

    public string? Notes { get; set; }

    public List<UnitCalculation> Units { get; set; } = new();
    public List<LoanCalculation> Loans { get; set; } = new();
    public List<CalculationScenario> Scenarios { get; set; } = new();
}
