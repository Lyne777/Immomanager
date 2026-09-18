namespace Immomanager.Web.Models;

/// <summary>Von Claude als Structured Output gelieferter Wochenspeiseplan - wird von
/// <see cref="Services.IMealPlanGenerationService"/> in ein <see cref="MealPlan"/> übersetzt.</summary>
public class MealPlanGenerationResult
{
    public List<GeneratedMealDay> Days { get; set; } = new();
}

public class GeneratedMealDay
{
    /// <summary>0-basierter Tagesabstand zum Startdatum der Anfrage.</summary>
    public int DayOffset { get; set; }

    public List<GeneratedMeal> Meals { get; set; } = new();
}

public class GeneratedMeal
{
    /// <summary>"Fruehstueck", "Mittag" oder "Abend" - siehe <see cref="MealType"/>.</summary>
    public string MealType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<GeneratedIngredient> Ingredients { get; set; } = new();
}

public class GeneratedIngredient
{
    public string Name { get; set; } = string.Empty;

    public decimal? Quantity { get; set; }

    public string? Unit { get; set; }

    public string Category { get; set; } = "Sonstiges";
}
