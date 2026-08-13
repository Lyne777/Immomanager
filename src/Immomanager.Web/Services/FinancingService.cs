using Immomanager.Web.Data;
using Immomanager.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Immomanager.Web.Services;

public class FinancingService : IFinancingService
{
    public static readonly Dictionary<LoanType, string> LoanTypeLabels = new()
    {
        [LoanType.Annuitaet] = "Annuität",
        [LoanType.Endfaellig] = "Endfällig",
        [LoanType.Sonstiges] = "Sonstiges",
    };

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public FinancingService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Financing?> GetByIdAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Financings.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<Financing> CreateAsync(Financing financing)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        db.Financings.Add(financing);
        await db.SaveChangesAsync();
        return financing;
    }

    public async Task UpdateAsync(Financing financing)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        db.Financings.Update(financing);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var financing = await db.Financings.FindAsync(id);
        if (financing is not null)
        {
            db.Financings.Remove(financing);
            await db.SaveChangesAsync();
        }
    }
}
