using Immomanager.Web.Data;
using Immomanager.Web.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace Immomanager.Web.Services;

/// <summary>Verwaltet Nebenkosten-/Betriebskostenabrechnungen je Immobilie(+Einheit) und
/// Abrechnungsjahr inkl. Kostenpositionen, Kennzahlen und portfolioweitem Vergleich.</summary>
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

    public async Task<List<UtilityStatement>> GetStatementsForPropertyAsync(int propertyId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.UtilityStatements
            .Where(s => s.PropertyId == propertyId)
            .Include(s => s.Items)
            .Include(s => s.Documents)
            .Include(s => s.PropertyUnit)
            .AsNoTracking()
            .OrderByDescending(s => s.Year)
            .ToListAsync();
    }

    public async Task<List<UtilityStatement>> GetStatementsForUnitAsync(int propertyUnitId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.UtilityStatements
            .Where(s => s.PropertyUnitId == propertyUnitId)
            .Include(s => s.Items)
            .Include(s => s.Documents)
            .AsNoTracking()
            .OrderByDescending(s => s.Year)
            .ToListAsync();
    }

    public async Task<UtilityStatement?> GetStatementAsync(int propertyId, int? propertyUnitId, int year)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.UtilityStatements
            .Where(s => s.PropertyId == propertyId && s.PropertyUnitId == propertyUnitId && s.Year == year)
            .Include(s => s.Items)
            .Include(s => s.Documents)
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }

    public async Task<UtilityStatement> UpsertStatementAsync(UtilityStatement statement)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var existing = await db.UtilityStatements.FirstOrDefaultAsync(s =>
            s.PropertyId == statement.PropertyId && s.PropertyUnitId == statement.PropertyUnitId && s.Year == statement.Year);

        if (existing is null)
        {
            db.UtilityStatements.Add(statement);
            await db.SaveChangesAsync();
            return statement;
        }

        existing.TotalCosts = statement.TotalCosts;
        existing.PeriodMonths = statement.PeriodMonths;
        existing.IsCompleted = statement.IsCompleted;

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

    public async Task<UtilityStatement> UploadStatementPdfAsync(int propertyId, int? propertyUnitId, int year, IBrowserFile file, CancellationToken cancellationToken = default)
    {
        if (file.ContentType != "application/pdf" && !file.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"\"{file.Name}\": Bitte eine PDF-Datei hochladen.");
        }

        if (file.Size > MaxPdfSizeBytes)
        {
            throw new InvalidOperationException($"\"{file.Name}\" ist zu groß (max. {MaxPdfSizeBytes / 1024 / 1024} MB).");
        }

        // Einheiten-Abrechnungen landen in einem eigenen Unterordner, damit sie nicht mit der
        // Ganzes-Objekt-Abrechnung um denselben Dateinamensraum konkurrieren.
        var storageFolder = propertyUnitId is null
            ? propertyId.ToString()
            : $"{propertyId}/units/{propertyUnitId}";

        var propertyDirectory = Path.Combine(_storageOptions.UtilityStatementsDirectoryAbsolute, storageFolder.Replace('/', Path.DirectorySeparatorChar));
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
            .FirstOrDefaultAsync(s => s.PropertyId == propertyId && s.PropertyUnitId == propertyUnitId && s.Year == year, cancellationToken);

        if (statement is null)
        {
            statement = new UtilityStatement { PropertyId = propertyId, PropertyUnitId = propertyUnitId, Year = year };
            db.UtilityStatements.Add(statement);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Bewusst als zusätzliches Dokument statt bestehende zu überschreiben - manche
        // Hausverwaltungen stellen je Einheit eine eigene, personalisierte Abrechnung aus statt
        // einer gemeinsamen Abrechnung fürs ganze Objekt.
        db.UtilityStatementDocuments.Add(new UtilityStatementDocument
        {
            UtilityStatementId = statement.Id,
            FilePath = $"{StorageOptions.UtilityStatementsRelativeRoot}/{storageFolder}/{storedFileName}",
            FileName = file.Name,
        });

        await db.SaveChangesAsync(cancellationToken);
        return statement;
    }

    public async Task DeleteStatementDocumentAsync(int documentId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var document = await db.UtilityStatementDocuments.FindAsync(documentId);
        if (document is null)
        {
            return;
        }

        db.UtilityStatementDocuments.Remove(document);
        await db.SaveChangesAsync();

        var absolutePath = Path.Combine(_storageOptions.DataDirectoryAbsolute, document.FilePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
    }

    /// <summary>Kennzahlen für eine einzelne Abrechnung. "totalCosts" bleibt als Rohwert stehen
    /// (tatsächlich abgerechneter Betrag für den Zeitraum), die €/m²- und €/Einheit-Werte werden
    /// intern auf 12 Monate hochgerechnet (siehe <see cref="BuildKpi"/>), damit eine Abrechnung über
    /// z. B. nur 6 Monate (typisch im Kauf-/Verkaufsjahr) nicht fälschlich als Halbjahreswert in den
    /// Jahres-/Monatskennzahlen landet.</summary>
    public UtilityStatementKpi CalculateKpi(int year, decimal totalCosts, decimal periodMonths, decimal areaSqm, int unitCount)
    {
        var months = periodMonths > 0 ? periodMonths : 12m;
        var annualizedTotalCosts = totalCosts / months * 12m;
        return BuildKpi(year, totalCosts, annualizedTotalCosts, areaSqm, unitCount);
    }

    private static UtilityStatementKpi BuildKpi(int year, decimal totalCosts, decimal annualizedTotalCosts, decimal areaSqm, int unitCount)
    {
        var kpi = new UtilityStatementKpi
        {
            Year = year,
            TotalCosts = totalCosts,
            UnitCount = unitCount,
            LivingAreaSqm = areaSqm,
        };

        kpi.CostPerUnitAnnual = unitCount > 0 ? annualizedTotalCosts / unitCount : 0;
        kpi.CostPerSqmAnnual = areaSqm > 0 ? annualizedTotalCosts / areaSqm : 0;
        kpi.CostPerSqmMonthly = kpi.CostPerSqmAnnual / 12;

        return kpi;
    }

    /// <summary>Annualisierte Summe über mehrere Abrechnungen (z. B. Ganzes Objekt + alle Einheiten
    /// eines Jahres) - jede Abrechnung wird zuerst einzeln auf 12 Monate hochgerechnet, bevor addiert
    /// wird, damit eine untypische Teilzeitraum-Abrechnung die Summe nicht verwässert.</summary>
    private static decimal SumAnnualized(IEnumerable<UtilityStatement> statements) =>
        statements.Sum(s => s.TotalCosts / (s.PeriodMonths > 0 ? s.PeriodMonths : 12m) * 12m);

    public async Task<UtilityStatementKpi> CalculatePropertyKpiAsync(Property property, int year)
    {
        var yearStatements = (await GetStatementsForPropertyAsync(property.Id)).Where(s => s.Year == year).ToList();
        var totalCosts = yearStatements.Sum(s => s.TotalCosts);
        var annualizedTotalCosts = SumAnnualized(yearStatements);
        return BuildKpi(year, totalCosts, annualizedTotalCosts, property.LivingAreaSqm, property.Units.Count);
    }

    public async Task<List<(Property Property, PropertyUnit Unit)>> GetUnitsMissingStatementAsync(IReadOnlyList<Property> properties, int year)
    {
        var relevantUnits = properties
            .SelectMany(p => p.Units.Where(u => u.CountsTowardRentTarget).Select(u => (Property: p, Unit: u)))
            .ToList();

        if (relevantUnits.Count == 0)
        {
            return new List<(Property, PropertyUnit)>();
        }

        await using var db = await _contextFactory.CreateDbContextAsync();
        var unitIds = relevantUnits.Select(x => x.Unit.Id).ToList();
        var unitIdsWithStatement = await db.UtilityStatements
            .Where(s => s.Year == year && s.PropertyUnitId != null && unitIds.Contains(s.PropertyUnitId!.Value))
            .Select(s => s.PropertyUnitId!.Value)
            .ToListAsync();

        return relevantUnits.Where(x => !unitIdsWithStatement.Contains(x.Unit.Id)).ToList();
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
        foreach (var property in properties)
        {
            var propertyStatements = statements.Where(s => s.PropertyId == property.Id).ToList();
            if (propertyStatements.Count == 0)
            {
                continue;
            }

            var totalCosts = propertyStatements.Sum(s => s.TotalCosts);
            var annualizedTotalCosts = SumAnnualized(propertyStatements);
            var kpi = BuildKpi(year, totalCosts, annualizedTotalCosts, property.LivingAreaSqm, property.Units.Count);
            rows.Add(new PortfolioUtilityComparisonRow
            {
                PropertyId = property.Id,
                PropertyName = property.Name,
                LivingAreaSqm = property.LivingAreaSqm,
                UnitCount = property.Units.Count,
                TotalCosts = totalCosts,
                CostPerSqmAnnual = kpi.CostPerSqmAnnual,
                CostPerSqmMonthly = kpi.CostPerSqmMonthly,
            });
        }

        return rows;
    }
}
