using System.ComponentModel.DataAnnotations;

namespace Immomanager.Web.Models;

/// <summary>Eine einzelne Mahlzeit (z. B. "Abendessen am 12.09.") innerhalb eines <see cref="MealPlan"/>.</summary>
public class PlannedMeal
{
    public int Id { get; set; }

    public int MealPlanId { get; set; }

    public MealPlan? MealPlan { get; set; }

    public DateOnly Date { get; set; }

    public MealType MealType { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Kurzes Rezept/Zubereitungshinweise.</summary>
    public string Description { get; set; } = string.Empty;

    [Range(1, 20)]
    public int Servings { get; set; } = 2;

    public List<MealIngredient> Ingredients { get; set; } = new();
}
