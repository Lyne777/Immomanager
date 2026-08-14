using System.Text.RegularExpressions;
using Immomanager.Web.Data;
using Immomanager.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Immomanager.Web.Services;

/// <summary>Verwaltet die manuellen Objekt-Logbuch-Einträge und führt sie mit den Renovierungsprojekten
/// zu einer gemeinsamen chronologischen Übersicht zusammen (siehe <see cref="GetCombinedLogAsync"/>).</summary>
public partial class PropertyLogService : IPropertyLogService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IRenovationService _renovationService;

    public PropertyLogService(IDbContextFactory<ApplicationDbContext> contextFactory, IRenovationService renovationService)
    {
        _contextFactory = contextFactory;
        _renovationService = renovationService;
    }

    public async Task<List<PropertyLogEntry>> GetEntriesAsync(int propertyId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.PropertyLogEntries
            .Where(e => e.PropertyId == propertyId)
            .Include(e => e.PropertyUnit)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<PropertyLogEntry> CreateAsync(PropertyLogEntry entry)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        db.PropertyLogEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    public async Task UpdateAsync(PropertyLogEntry entry)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        db.PropertyLogEntries.Update(entry);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int entryId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var entry = await db.PropertyLogEntries.FindAsync(entryId);
        if (entry is not null)
        {
            db.PropertyLogEntries.Remove(entry);
            await db.SaveChangesAsync();
        }
    }

    public async Task<List<PropertyLogItem>> GetCombinedLogAsync(int propertyId)
    {
        var manualEntries = await GetEntriesAsync(propertyId);
        var renovations = await _renovationService.GetProjectsByPropertyIdAsync(propertyId);

        var items = new List<PropertyLogItem>();

        items.AddRange(manualEntries.Select(e => new PropertyLogItem
        {
            Source = PropertyLogItemSource.Manual,
            SortYear = ExtractYear(e.DateLabel),
            DateLabel = e.DateLabel,
            Description = e.Description,
            UnitLabel = e.PropertyUnit?.Label,
            ManualEntryId = e.Id,
        }));

        items.AddRange(renovations.Select(r => new PropertyLogItem
        {
            Source = PropertyLogItemSource.Renovation,
            SortYear = r.StartDate.Year,
            DateLabel = r.StartDate.ToString("MM/yyyy"),
            Description = $"{r.Name} ({RenovationAnalyticsService.CategoryDisplayNames[r.Category]})",
            Cost = r.ActualTotalCost,
            RenovationProjectId = r.Id,
        }));

        return items.OrderBy(i => i.SortYear).ThenBy(i => i.Description).ToList();
    }

    /// <summary>Erkennt das erste vierstellige Jahr (19xx/20xx) im Freitext, z. B. "1998/1999" -> 1998.
    /// Liefert 0, falls kein Jahr erkennbar war (solche Einträge landen dann chronologisch zuerst).</summary>
    private static int ExtractYear(string dateLabel)
    {
        var match = YearPattern().Match(dateLabel);
        return match.Success ? int.Parse(match.Value) : 0;
    }

    [GeneratedRegex(@"(19|20)\d{2}")]
    private static partial Regex YearPattern();
}
