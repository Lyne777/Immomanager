using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public interface IPropertyService
{
    Task<List<Property>> GetAllAsync();
    Task<Property?> GetByIdAsync(int id);
    Task<Property> CreateAsync(Property property);
    Task UpdateAsync(Property property);
    Task DeleteAsync(int id);
}
