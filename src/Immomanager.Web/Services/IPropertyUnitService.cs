using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public interface IPropertyUnitService
{
    /// <summary>Lädt eine Einheit inkl. Mietverhältnissen und übergeordneter Immobilie (für die
    /// Einheiten-Detailseite).</summary>
    Task<PropertyUnit?> GetByIdAsync(int id);

    Task<PropertyUnit> CreateAsync(PropertyUnit unit);

    Task UpdateAsync(PropertyUnit unit);

    Task DeleteAsync(int id);
}
