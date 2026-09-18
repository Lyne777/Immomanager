using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public interface IMealPlanService
{
    Task<List<MealPlan>> GetAllAsync();
    Task<MealPlan?> GetByIdAsync(int id);
    Task<MealPlan> CreateAsync(MealPlan plan);
    Task DeleteAsync(int id);
}
