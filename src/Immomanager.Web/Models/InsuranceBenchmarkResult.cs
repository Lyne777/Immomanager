namespace Immomanager.Web.Models;

/// <summary>Ergebnis der Richtwert-Analyse für eine Versicherungskategorie einer Immobilie.</summary>
public class InsuranceBenchmarkResult
{
    public InsuranceCategory Category { get; set; }

    public decimal AnnualPremium { get; set; }

    public int UnitCount { get; set; }

    public decimal LivingAreaSqm { get; set; }

    public decimal CostPerUnitAnnual { get; set; }

    public decimal CostPerSqmAnnual { get; set; }

    public decimal BenchmarkMinPerUnit { get; set; }

    public decimal BenchmarkMaxPerUnit { get; set; }

    public InsuranceBenchmarkStatus Status { get; set; }
}
