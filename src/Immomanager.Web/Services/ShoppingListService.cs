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

    public async Task<ShoppingList> GenerateForPlanAsync(int mealPlanId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var plan = await db.MealPlans
            .Include(p => p.Meals).ThenInclude(m => m.Ingredients)
            .Include(p => p.ShoppingList).ThenInclude(s => s!.Items)
            .FirstOrDefaultAsync(p => p.Id == mealPlanId)
            ?? throw new InvalidOperationException($"Speiseplan mit Id {mealPlanId} wurde nicht gefunden.");

        var aggregated = Aggregate(plan.Meals.SelectMany(m => m.Ingredients));

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
                    Quantity = canSum ? items.Sum(i => i.Quantity!.Value) : null,
                };
            })
            .OrderBy(i => i.Category)
            .ThenBy(i => i.Name)
            .ToList();
    }
}
