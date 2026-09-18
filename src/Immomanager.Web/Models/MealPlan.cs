using System.ComponentModel.DataAnnotations;

namespace Immomanager.Web.Models;

/// <summary>Ein von der KI generierter, gespeicherter Wochenspeiseplan für den Haushalt.</summary>
public class MealPlan
{
    public int Id { get; set; }

    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(500)]
    public string? Notes { get; set; }

    public List<PlannedMeal> Meals { get; set; } = new();

    public ShoppingList? ShoppingList { get; set; }
}
