namespace Immomanager.Web.Models;

/// <summary>Von Claude aus einer Nebenkostenabrechnung extrahierte Daten.</summary>
public class UtilityStatementAnalysisResult
{
    public int? Year { get; set; }

    public decimal? TotalCosts { get; set; }

    public string? Summary { get; set; }

    public List<UtilityCostItemFinding> CostItems { get; set; } = new();
}

/// <summary>Eine von Claude aus der Abrechnung extrahierte Kostenposition. "Category" entspricht
/// einem <see cref="UtilityCostCategory"/>-Namen (String, da Structured Outputs kein natives Enum
/// kennt).</summary>
public class UtilityCostItemFinding
{
    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}
