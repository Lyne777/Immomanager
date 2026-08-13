using Immomanager.Web.Data;
using Immomanager.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Immomanager.Web.Services;

public class RentTargetService : IRentTargetService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public RentTargetService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<RentTarget>> GetByPropertyIdAsync(int propertyId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.RentTargets
            .Where(t => t.PropertyId == propertyId)
            .OrderBy(t => t.Year).ThenBy(t => t.Quarter)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<RentTarget> CreateAsync(RentTarget target)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        await EnsureNoDuplicateAsync(db, target);
        db.RentTargets.Add(target);
        await db.SaveChangesAsync();
        return target;
    }

    public async Task UpdateAsync(RentTarget target)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        await EnsureNoDuplicateAsync(db, target);
        db.RentTargets.Update(target);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var target = await db.RentTargets.FindAsync(id);
        if (target is not null)
        {
            db.RentTargets.Remove(target);
            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureNoDuplicateAsync(ApplicationDbContext db, RentTarget target)
    {
        var duplicateExists = await db.RentTargets.AnyAsync(t =>
            t.Id != target.Id && t.PropertyId == target.PropertyId && t.Year == target.Year && t.Quarter == target.Quarter);

        if (duplicateExists)
        {
            throw new InvalidOperationException(
                $"Für Q{target.Quarter} {target.Year} existiert bei dieser Immobilie bereits ein Soll-Wert - bitte den vorhandenen Eintrag bearbeiten.");
        }
    }
}
