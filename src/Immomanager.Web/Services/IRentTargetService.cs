using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public interface IRentTargetService
{
    Task<List<RentTarget>> GetByPropertyIdAsync(int propertyId);
    Task<RentTarget> CreateAsync(RentTarget target);
    Task UpdateAsync(RentTarget target);
    Task DeleteAsync(int id);
}
