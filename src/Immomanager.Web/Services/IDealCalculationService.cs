using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public interface IDealCalculationService
{
    Task<List<DealCalculation>> GetAllAsync();
    Task<DealCalculation?> GetByIdAsync(int id);
    Task<DealCalculation> CreateAsync(DealCalculation deal);
    Task UpdateAsync(DealCalculation deal);
    Task DeleteAsync(int id);
    Task<DealCalculation> DuplicateAsync(int id);
}
