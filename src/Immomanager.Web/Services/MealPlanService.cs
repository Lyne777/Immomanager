using Immomanager.Web.Data;
using Immomanager.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Immomanager.Web.Services;

public class MealPlanService : IMealPlanService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public MealPlanService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<MealPlan>> GetAllAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.MealPlans
            .Include(p => p.Meals)
            .AsNoTracking()
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<MealPlan?> GetByIdAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.MealPlans
            .Include(p => p.Meals).ThenInclude(m => m.Ingredients)
            .Include(p => p.ShoppingList).ThenInclude(s => s!.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<MealPlan> CreateAsync(MealPlan plan)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        db.MealPlans.Add(plan);
        await db.SaveChangesAsync();
        return plan;
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var plan = await db.MealPlans.FindAsync(id);
        if (plan is not null)
        {
            db.MealPlans.Remove(plan);
            await db.SaveChangesAsync();
        }
    }
}
