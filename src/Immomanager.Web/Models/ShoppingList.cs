namespace Immomanager.Web.Models;

/// <summary>Aus den Zutaten eines <see cref="MealPlan"/> aggregierte Einkaufsliste - höchstens eine
/// je Plan (wird beim erneuten Erstellen komplett neu befüllt).</summary>
public class ShoppingList
{
    public int Id { get; set; }

    public int MealPlanId { get; set; }

    public MealPlan? MealPlan { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Für wie viele Personen die Mengen der Positionen umgerechnet wurden (die Rezeptmengen
    /// selbst gelten für <see cref="PlannedMeal.Servings"/> Portionen).</summary>
    public int Persons { get; set; } = PlannedMeal.DefaultServings;

    public List<ShoppingListItem> Items { get; set; } = new();
}
