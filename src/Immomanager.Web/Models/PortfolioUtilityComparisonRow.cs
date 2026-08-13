namespace Immomanager.Web.Models;

/// <summary>Eine Zeile im portfolioweiten Nebenkosten-Vergleich (Dashboard) für ein Abrechnungsjahr.</summary>
public class PortfolioUtilityComparisonRow
{
    public int PropertyId { get; set; }

    public string PropertyName { get; set; } = string.Empty;

    public decimal LivingAreaSqm { get; set; }

    public int UnitCount { get; set; }

    public decimal TotalCosts { get; set; }

    public decimal CostPerSqmAnnual { get; set; }

    public decimal CostPerSqmMonthly { get; set; }
}
