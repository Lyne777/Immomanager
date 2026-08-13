using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public interface IFinancingService
{
    Task<Financing?> GetByIdAsync(int id);
    Task<Financing> CreateAsync(Financing financing);
    Task UpdateAsync(Financing financing);
    Task DeleteAsync(int id);
}
