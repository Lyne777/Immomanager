using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public interface IPropertyLogService
{
    Task<List<PropertyLogEntry>> GetEntriesAsync(int propertyId);

    Task<PropertyLogEntry> CreateAsync(PropertyLogEntry entry);

    Task UpdateAsync(PropertyLogEntry entry);

    Task DeleteAsync(int entryId);

    /// <summary>Führt die manuellen Logbuch-Einträge und die Renovierungsprojekte der Immobilie zu einer
    /// gemeinsamen, chronologischen Liste zusammen (aufsteigend nach erkanntem Jahr).</summary>
    Task<List<PropertyLogItem>> GetCombinedLogAsync(int propertyId);
}
