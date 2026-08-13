namespace Immomanager.Web.Models;

/// <summary>Berechnete Kennzahlen einer Nebenkostenabrechnung für ein Abrechnungsjahr.</summary>
public class UtilityStatementKpi
{
    public int Year { get; set; }

    public decimal TotalCosts { get; set; }

    public int UnitCount { get; set; }

    public decimal LivingAreaSqm { get; set; }

    public decimal CostPerUnitAnnual { get; set; }

    public decimal CostPerSqmAnnual { get; set; }

    public decimal CostPerSqmMonthly { get; set; }
}
