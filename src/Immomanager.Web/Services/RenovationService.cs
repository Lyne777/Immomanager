using Immomanager.Web.Data;
using Immomanager.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Immomanager.Web.Services;

public class RenovationService : IRenovationService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public RenovationService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<RenovationProject>> GetProjectsByPropertyIdAsync(int propertyId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.RenovationProjects
            .Where(r => r.PropertyId == propertyId)
            .Include(r => r.LineItems)
            .AsNoTracking()
            .OrderByDescending(r => r.StartDate)
            .ToListAsync();
    }

    public async Task<RenovationProject?> GetProjectByIdAsync(int projectId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.RenovationProjects
            .Include(r => r.LineItems)
            .Include(r => r.Property)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == projectId);
    }

    public async Task<List<RenovationProject>> GetAllProjectsAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.RenovationProjects
            .Include(r => r.LineItems)
            .Include(r => r.Property)
            .AsNoTracking()
            .OrderByDescending(r => r.StartDate)
            .ToListAsync();
    }

    public async Task<RenovationProject> CreateProjectAsync(RenovationProject project)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        db.RenovationProjects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    public async Task UpdateProjectAsync(RenovationProject project)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        db.RenovationProjects.Update(project);
        await db.SaveChangesAsync();
    }

    public async Task DeleteProjectAsync(int projectId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var project = await db.RenovationProjects.FindAsync(projectId);
        if (project is not null)
        {
            db.RenovationProjects.Remove(project);
            await db.SaveChangesAsync();
        }
    }

    public async Task<RenovationLineItem> CreateLineItemAsync(RenovationLineItem lineItem)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        db.RenovationLineItems.Add(lineItem);
        await db.SaveChangesAsync();
        return lineItem;
    }

    public async Task UpdateLineItemAsync(RenovationLineItem lineItem)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        db.RenovationLineItems.Update(lineItem);
        await db.SaveChangesAsync();
    }

    public async Task DeleteLineItemAsync(int lineItemId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var lineItem = await db.RenovationLineItems.FindAsync(lineItemId);
        if (lineItem is not null)
        {
            db.RenovationLineItems.Remove(lineItem);
            await db.SaveChangesAsync();
        }
    }
}
