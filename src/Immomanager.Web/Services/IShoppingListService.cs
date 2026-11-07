using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public interface IShoppingListService
{
    /// <summary>Erlaubter Bereich der Personenzahl für die Mengenumrechnung.</summary>
    public const int MinPersons = 1;

    public const int MaxPersons = 12;

    Task<ShoppingList?> GetByIdAsync(int id);

    /// <summary>Aggregiert die Zutaten aller Mahlzeiten des Plans zu einer Einkaufsliste und rechnet die
    /// Mengen von der Portionenzahl der Rezepte (<see cref="PlannedMeal.Servings"/>) auf
    /// <paramref name="persons"/> Personen um. Existiert bereits eine Liste für diesen Plan, wird sie
    /// komplett neu befüllt (Abhak-Status geht dabei verloren - das ist beabsichtigt, da sich sonst der
    /// Zutaten-Zuschnitt nicht sauber abgleichen ließe).</summary>
    Task<ShoppingList> GenerateForPlanAsync(int mealPlanId, int persons);

    Task ToggleCheckedAsync(int itemId);
}
