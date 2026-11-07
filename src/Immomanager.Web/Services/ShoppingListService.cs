using Immomanager.Web.Data;
using Immomanager.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Immomanager.Web.Services;

public class ShoppingListService : IShoppingListService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public ShoppingListService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<ShoppingList?> GetByIdAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.ShoppingLists
            .Include(s => s.Items)
            .Include(s => s.MealPlan)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<ShoppingList> GenerateForPlanAsync(int mealPlanId, int persons)
    {
        if (persons is < IShoppingListService.MinPersons or > IShoppingListService.MaxPersons)
        {
            throw new InvalidOperationException(
                $"Die Personenzahl muss zwischen {IShoppingListService.MinPersons} und {IShoppingListService.MaxPersons} liegen.");
        }

        await using var db = await _contextFactory.CreateDbContextAsync();

        var plan = await db.MealPlans
            .Include(p => p.Meals).ThenInclude(m => m.Ingredients)
            .Include(p => p.ShoppingList).ThenInclude(s => s!.Items)
            .FirstOrDefaultAsync(p => p.Id == mealPlanId)
            ?? throw new InvalidOperationException($"Speiseplan mit Id {mealPlanId} wurde nicht gefunden.");

        var aggregated = Aggregate(plan.Meals.SelectMany(meal => ScaleIngredients(meal, persons)));

        if (plan.ShoppingList is null)
        {
            plan.ShoppingList = new ShoppingList { MealPlanId = mealPlanId };
            db.ShoppingLists.Add(plan.ShoppingList);
        }
        else
        {
            db.ShoppingListItems.RemoveRange(plan.ShoppingList.Items);
            plan.ShoppingList.Items.Clear();
        }

        plan.ShoppingList.Persons = persons;

        foreach (var item in aggregated)
        {
            plan.ShoppingList.Items.Add(item);
        }

        await db.SaveChangesAsync();
        return plan.ShoppingList;
    }

    public async Task ToggleCheckedAsync(int itemId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var item = await db.ShoppingListItems.FindAsync(itemId)
            ?? throw new InvalidOperationException($"Position mit Id {itemId} wurde nicht gefunden.");
        item.IsChecked = !item.IsChecked;
        await db.SaveChangesAsync();
    }

    // Einheiten, die man nur ganz kaufen kann: umgerechnete Mengen werden aufgerundet (4,5 Stück -> 5).
    private static readonly HashSet<string> CountableUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "Stück", "Stk", "Stk.", "Dose", "Dosen", "Bund", "Packung", "Packungen", "Pck", "Pck.", "Becher",
        "Kopf", "Zehe", "Zehen", "Glas", "Gläser", "Flasche", "Flaschen",
    };

    /// <summary>Zutaten einer Mahlzeit mit auf <paramref name="persons"/> umgerechneten Mengen. Die
    /// Rezeptmengen gelten für <see cref="PlannedMeal.Servings"/> Portionen (eine je Person); Zutaten
    /// ohne Menge bleiben unverändert.</summary>
    private static IEnumerable<MealIngredient> ScaleIngredients(PlannedMeal meal, int persons)
    {
        var factor = (decimal)persons / Math.Max(1, meal.Servings);
        return meal.Ingredients.Select(i => new MealIngredient
        {
            Name = i.Name,
            Unit = i.Unit,
            Category = i.Category,
            Quantity = i.Quantity.HasValue ? i.Quantity.Value * factor : null,
        });
    }

    private static decimal RoundForShopping(decimal quantity, string? unit)
    {
        var trimmedUnit = unit?.Trim() ?? string.Empty;
        if (CountableUnits.Contains(trimmedUnit))
        {
            return Math.Ceiling(quantity);
        }

        // Gramm/Milliliter sind in der Praxis ohnehin ganzzahlig; alles andere (kg, l, ...) auf 2 Stellen.
        var rounded = trimmedUnit.Equals("g", StringComparison.OrdinalIgnoreCase) || trimmedUnit.Equals("ml", StringComparison.OrdinalIgnoreCase)
            ? Math.Round(quantity, 0, MidpointRounding.AwayFromZero)
            : Math.Round(quantity, 2, MidpointRounding.AwayFromZero);

        // Eine positive Menge (z. B. 0,4 g Safran) darf nie zu "0 g" werden: dann lieber die Nachkommastelle behalten.
        return rounded <= 0 && quantity > 0
            ? Math.Max(0.01m, Math.Round(quantity, 2, MidpointRounding.AwayFromZero))
            : rounded;
    }

    private static List<ShoppingListItem> Aggregate(IEnumerable<MealIngredient> ingredients)
    {
        return ingredients
            .GroupBy(i => (Name: i.Name.Trim().ToLowerInvariant(), Unit: i.Unit?.Trim().ToLowerInvariant() ?? string.Empty))
            .Select(group =>
            {
                var items = group.ToList();
                var canSum = items.All(i => i.Quantity.HasValue);
                return new ShoppingListItem
                {
                    Name = items[0].Name.Trim(),
                    Unit = items[0].Unit,
                    Category = items[0].Category,
                    Quantity = canSum ? RoundForShopping(items.Sum(i => i.Quantity!.Value), items[0].Unit) : null,
                };
            })
            .OrderBy(i => i.Category)
            .ThenBy(i => i.Name)
            .ToList();
    }
}
