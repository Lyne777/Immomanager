using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public interface IShoppingListService
{
    Task<ShoppingList?> GetByIdAsync(int id);

    /// <summary>Aggregiert die Zutaten aller Mahlzeiten des Plans zu einer Einkaufsliste. Existiert
    /// bereits eine Liste für diesen Plan, wird sie komplett neu befüllt (Abhak-Status geht dabei
    /// verloren - das ist beabsichtigt, da sich sonst der Zutaten-Zuschnitt nicht sauber abgleichen ließe).</summary>
    Task<ShoppingList> GenerateForPlanAsync(int mealPlanId);

    Task ToggleCheckedAsync(int itemId);
}
