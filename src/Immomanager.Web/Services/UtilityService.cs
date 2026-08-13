using Immomanager.Web.Data;
using Immomanager.Web.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace Immomanager.Web.Services;

/// <summary>Verwaltet Nebenkosten-/Betriebskostenabrechnungen je Immobilie und Abrechnungsjahr
/// inkl. Kostenpositionen, Kennzahlen und portfolioweitem Vergleich.</summary>
public class UtilityService : IUtilityService
{
    public const long MaxPdfSizeBytes = 20 * 1024 * 1024;

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly StorageOptions _storageOptions;

    public UtilityService(IDbContextFactory<ApplicationDbContext> contextFactory, StorageOptions storageOptions)
    {
        _contextFactory = contextFactory;
        _storageOptions = storageOptions;
    }

    public async Task<List<UtilityStatement>> GetStatementsAsync(int propertyId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.UtilityStatements
            .Where(s => s.PropertyId == propertyId)
            .Include(s => s.Items)
            .AsNoTracking()
            .OrderByDescending(s => s.Year)
            .ToListAsync();
    }

    public async Task<UtilityStatement?> GetStatementAsync(int propertyId, int year)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.UtilityStatements
            .Where(s => s.PropertyId == propertyId && s.Year == year)
            .Include(s => s.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }

    public async Task<UtilityStatement> UpsertStatementAsync(UtilityStatement statement)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var existing = await db.UtilityStatements
            .FirstOrDefaultAsync(s => s.PropertyId == statement.PropertyId && s.Year == statement.Year);

        if (existing is null)
        {
            db.UtilityStatements.Add(statement);
            await db.SaveChangesAsync();
            return statement;
        }

        existing.TotalCosts = statement.TotalCosts;
        existing.IsCompleted = statement.IsCompleted;
        // PdfFilePath/PdfFileName werden separat über UploadStatementPdfAsync gepflegt, hier nicht
        // überschreiben, falls "statement" (z. B. aus einem reinen Bearbeiten-Dialog) diese Felder nicht kennt.
        if (statement.PdfFilePath is not null)
        {
            existing.PdfFilePath = statement.PdfFilePath;
            existing.PdfFileName = statement.PdfFileName;
        }

        await db.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteStatementAsync(int statementId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var statement = await db.UtilityStatements.FindAsync(statementId);
        if (statement is not null)
        {
            db.UtilityStatements.Remove(statement);
            await db.SaveChangesAsync();
        }
    }

    public async Task<UtilityCostItem> CreateItemAsync(UtilityCostItem item)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        db.UtilityCostItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task UpdateItemAsync(UtilityCostItem item)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        db.UtilityCostItems.Update(item);
        await db.SaveChangesAsync();
    }

    public async Task DeleteItemAsync(int itemId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var item = await db.UtilityCostItems.FindAsync(itemId);
        if (item is not null)
        {
            db.UtilityCostItems.Remove(item);
            await db.SaveChangesAsync();
        }
    }

    public async Task<UtilityStatement> UploadStatementPdfAsync(int propertyId, int year, IBrowserFile file, CancellationToken cancellationToken = default)
    {
        if (file.ContentType != "application/pdf" && !file.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"\"{file.Name}\": Bitte eine PDF-Datei hochladen.");
        }

        if (file.Size > MaxPdfSizeBytes)
        {
            throw new InvalidOperationException($"\"{file.Name}\" ist zu groß (max. {MaxPdfSizeBytes / 1024 / 1024} MB).");
        }

        var propertyDirectory = Path.Combine(_storageOptions.UtilityStatementsDirectoryAbsolute, propertyId.ToString());
        Directory.CreateDirectory(propertyDirectory);

        var storedFileName = $"{year}_{Guid.NewGuid():N}.pdf";
        var absolutePath = Path.Combine(propertyDirectory, storedFileName);

        await using (var fileStream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write))
        await using (var browserStream = file.OpenReadStream(MaxPdfSizeBytes, cancellationToken))
        {
            await browserStream.CopyToAsync(fileStream, cancellationToken);
        }

        await using var db = await _contextFactory.CreateDbContextAsync();
        var statement = await db.UtilityStatements
            .FirstOrDefaultAsync(s => s.PropertyId == propertyId && s.Year == year, cancellationToken);

        if (statement is null)
        {
            statement = new UtilityStatement { PropertyId = propertyId, Year = year };
            db.UtilityStatements.Add(statement);
        }

        statement.PdfFilePath = $"{StorageOptions.UtilityStatementsRelativeRoot}/{propertyId}/{storedFileName}";
        statement.PdfFileName = file.Name;

        await db.SaveChangesAsync(cancellationToken);
        return statement;
    }

    public UtilityStatementKpi CalculateKpi(Property property, UtilityStatement statement)
    {
        var kpi = new UtilityStatementKpi
        {
            Year = statement.Year,
            TotalCosts = statement.TotalCosts,
            UnitCount = property.Units.Count,
            LivingAreaSqm = property.LivingAreaSqm,
        };

        kpi.CostPerUnitAnnual = kpi.UnitCount > 0 ? statement.TotalCosts / kpi.UnitCount : 0;
        kpi.CostPerSqmAnnual = property.LivingAreaSqm > 0 ? statement.TotalCosts / property.LivingAreaSqm : 0;
        kpi.CostPerSqmMonthly = kpi.CostPerSqmAnnual / 12;

        return kpi;
    }

    public async Task<List<Property>> GetPropertiesMissingStatementAsync(IReadOnlyList<Property> properties, int year)
    {
        if (properties.Count == 0)
        {
            return new List<Property>();
        }

        await using var db = await _contextFactory.CreateDbContextAsync();
        var propertyIds = properties.Select(p => p.Id).ToList();
        var propertyIdsWithStatement = await db.UtilityStatements
            .Where(s => s.Year == year && propertyIds.Contains(s.PropertyId))
            .Select(s => s.PropertyId)
            .ToListAsync();

        return properties.Where(p => !propertyIdsWithStatement.Contains(p.Id)).ToList();
    }

    public async Task<List<PortfolioUtilityComparisonRow>> GetPortfolioComparisonAsync(IReadOnlyList<Property> properties, int year)
    {
        if (properties.Count == 0)
        {
            return new List<PortfolioUtilityComparisonRow>();
        }

        await using var db = await _contextFactory.CreateDbContextAsync();
        var propertyIds = properties.Select(p => p.Id).ToList();
        var statements = await db.UtilityStatements
            .Where(s => s.Year == year && propertyIds.Contains(s.PropertyId))
            .AsNoTracking()
            .ToListAsync();

        var rows = new List<PortfolioUtilityComparisonRow>();
        foreach (var statement in statements)
        {
            var property = properties.FirstOrDefault(p => p.Id == statement.PropertyId);
            if (property is null)
            {
                continue;
            }

            var kpi = CalculateKpi(property, statement);
            rows.Add(new PortfolioUtilityComparisonRow
            {
                PropertyId = property.Id,
                PropertyName = property.Name,
                LivingAreaSqm = property.LivingAreaSqm,
                UnitCount = property.Units.Count,
                TotalCosts = statement.TotalCosts,
                CostPerSqmAnnual = kpi.CostPerSqmAnnual,
                CostPerSqmMonthly = kpi.CostPerSqmMonthly,
            });
        }

        return rows;
    }
}
