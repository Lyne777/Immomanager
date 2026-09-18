using System.ComponentModel.DataAnnotations;

namespace Immomanager.Web.Models;

/// <summary>Eine Zutat einer <see cref="PlannedMeal"/> - Grundlage für die spätere Aggregation zur
/// Einkaufsliste (siehe <see cref="ShoppingListItem"/>).</summary>
public class MealIngredient
{
    public int Id { get; set; }

    public int PlannedMealId { get; set; }

    public PlannedMeal? PlannedMeal { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public decimal? Quantity { get; set; }

    [StringLength(30)]
    public string? Unit { get; set; }

    [Required, StringLength(50)]
    public string Category { get; set; } = "Sonstiges";
}
