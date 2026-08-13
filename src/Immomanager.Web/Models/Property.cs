using System.ComponentModel.DataAnnotations;

namespace Immomanager.Web.Models;

/// <summary>Eine Immobilie/Einheit im Portfolio.</summary>
public class Property
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string Address { get; set; } = string.Empty;

    public int? YearBuilt { get; set; }

    public DateOnly PurchaseDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Kaufpreis netto.</summary>
    [Range(0, double.MaxValue)]
    public decimal PurchasePrice { get; set; }

    /// <summary>Grunderwerbsteuer.</summary>
    [Range(0, double.MaxValue)]
    public decimal PropertyTransferTax { get; set; }

    /// <summary>Notar- und Grundbuchkosten.</summary>
    [Range(0, double.MaxValue)]
    public decimal NotaryAndRegistryCosts { get; set; }

    /// <summary>Maklerprovision.</summary>
    [Range(0, double.MaxValue)]
    public decimal BrokerCommission { get; set; }

    /// <summary>Modernisierungskosten beim Kauf.</summary>
    [Range(0, double.MaxValue)]
    public decimal InitialRenovationCosts { get; set; }

    /// <summary>Beteiligungsquote in Prozent (0-100). 100 = Alleineigentum.</summary>
    [Range(0.01, 100)]
    public decimal OwnershipSharePercent { get; set; } = 100m;

    /// <summary>Aktueller geschätzter Marktwert (Gesamtobjekt).</summary>
    [Range(0, double.MaxValue)]
    public decimal CurrentMarketValue { get; set; }

    public string? Notes { get; set; }

    public List<Financing> Financings { get; set; } = new();

    public List<PropertyImage> Images { get; set; } = new();

    public List<RenovationProject> RenovationProjects { get; set; } = new();

    public List<RentTarget> RentTargets { get; set; } = new();

    public List<PropertyUnit> Units { get; set; } = new();

    public List<InsurancePolicy> InsurancePolicies { get; set; } = new();

    public List<InsuranceCheckItem> InsuranceCheckItems { get; set; } = new();

    public List<UtilityStatement> UtilityStatements { get; set; } = new();

    public List<PropertyDocument> Documents { get; set; } = new();

    /// <summary>Wohn-/Nutzfläche gesamt - ergibt sich aus der Summe der Einheiten
    /// (<see cref="Units"/>), damit sie nicht getrennt von den Einzelwerten gepflegt werden muss.</summary>
    public decimal LivingAreaSqm => Units.Sum(u => u.AreaSqm);

    /// <summary>Aktuelle Kaltmiete pro Monat (Gesamtobjekt) - Summe aus den Einheiten.</summary>
    public decimal CurrentColdRentMonthly => Units.Sum(u => u.ColdRentMonthly);

    /// <summary>Nicht umlegbare Kosten pro Monat (Gesamtobjekt) - Summe aus den Einheiten.</summary>
    public decimal NonAllocableCostsMonthly => Units.Sum(u => u.NonAllocableCostsMonthly);

    public decimal TotalAcquisitionCosts =>
        PropertyTransferTax + NotaryAndRegistryCosts + BrokerCommission + InitialRenovationCosts;

    public decimal TotalInvestment => PurchasePrice + TotalAcquisitionCosts;
}
