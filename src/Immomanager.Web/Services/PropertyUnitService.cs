using Immomanager.Web.Data;
using Immomanager.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Immomanager.Web.Services;

public class PropertyUnitService : IPropertyUnitService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public PropertyUnitService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<PropertyUnit?> GetByIdAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.PropertyUnits
            .Include(u => u.Property)
            .Include(u => u.Tenancies)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<PropertyUnit> CreateAsync(PropertyUnit unit)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        db.PropertyUnits.Add(unit);
        await db.SaveChangesAsync();
        return unit;
    }

    public async Task UpdateAsync(PropertyUnit unit)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        db.PropertyUnits.Update(unit);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var unit = await db.PropertyUnits.FindAsync(id);
        if (unit is not null)
        {
            db.PropertyUnits.Remove(unit);
            await db.SaveChangesAsync();
        }
    }
}
