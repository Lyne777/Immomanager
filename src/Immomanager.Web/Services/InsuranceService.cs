using Immomanager.Web.Data;
using Immomanager.Web.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace Immomanager.Web.Services;

/// <summary>Verwaltet Versicherungs-Policen (Vertragsfakten je Immobilie+Kategorie) und die davon
/// unabhängige Prüf-Checkliste (direkt an die Immobilie gebunden), inkl. Richtwert-Benchmark.</summary>
public class InsuranceService : IInsuranceService
{
    public const long MaxPdfSizeBytes = 20 * 1024 * 1024;

    public static readonly Dictionary<InsuranceCategory, string> CategoryDisplayNames = new()
    {
        [InsuranceCategory.Gebaeudeversicherung] = "Gebäudeversicherung",
        [InsuranceCategory.HausUndGrundbesitzerhaftpflicht] = "Haus- und Grundbesitzerhaftpflicht",
    };

    private static readonly Dictionary<InsuranceCategory, (decimal Min, decimal Max)> BenchmarkRanges = new()
    {
        [InsuranceCategory.Gebaeudeversicherung] = (120m, 250m),
        [InsuranceCategory.HausUndGrundbesitzerhaftpflicht] = (15m, 35m),
    };

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly StorageOptions _storageOptions;

    public InsuranceService(IDbContextFactory<ApplicationDbContext> contextFactory, StorageOptions storageOptions)
    {
        _contextFactory = contextFactory;
        _storageOptions = storageOptions;
    }

    public async Task<List<InsuranceCheckItem>> GetCheckItemsAsync(int propertyId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var existingKeys = await db.InsuranceCheckItems
            .Where(c => c.PropertyId == propertyId)
            .Select(c => c.Key)
            .ToListAsync();

        var missingTemplates = InsuranceCheckCatalog.Items
            .Where(t => !existingKeys.Contains(t.Key))
            .ToList();

        if (missingTemplates.Count > 0)
        {
            foreach (var template in missingTemplates)
            {
                db.InsuranceCheckItems.Add(new InsuranceCheckItem
                {
                    PropertyId = propertyId,
                    Key = template.Key,
                    Category = template.Category,
                    GroupLabel = template.GroupLabel,
                    Title = template.Title,
                    SortOrder = template.SortOrder,
                });
            }

            await db.SaveChangesAsync();
        }

        return await db.InsuranceCheckItems
            .Where(c => c.PropertyId == propertyId)
            .AsNoTracking()
            .OrderBy(c => c.Category).ThenBy(c => c.SortOrder)
            .ToListAsync();
    }

    public async Task UpdateCheckItemAsync(InsuranceCheckItem item)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        db.InsuranceCheckItems.Update(item);
        await db.SaveChangesAsync();
    }

    public async Task<List<InsurancePolicy>> GetPoliciesAsync(int propertyId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.InsurancePolicies
            .Where(i => i.PropertyId == propertyId)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<InsurancePolicy?> GetPolicyAsync(int propertyId, InsuranceCategory category)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.InsurancePolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.PropertyId == propertyId && i.Category == category);
    }

    public async Task<InsurancePolicy> UpsertPolicyAsync(InsurancePolicy policy)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var existing = await db.InsurancePolicies
            .FirstOrDefaultAsync(i => i.PropertyId == policy.PropertyId && i.Category == policy.Category);

        if (existing is null)
        {
            db.InsurancePolicies.Add(policy);
            await db.SaveChangesAsync();
            return policy;
        }

        existing.Provider = policy.Provider;
        existing.PolicyNumber = policy.PolicyNumber;
        existing.AnnualPremium = policy.AnnualPremium;
        existing.StartDate = policy.StartDate;
        existing.ExpirationDate = policy.ExpirationDate;
        // PdfFilePath/PdfFileName werden separat über UploadPolicyPdfAsync gepflegt, hier nicht
        // überschreiben, falls policy (z. B. aus einem reinen Bearbeiten-Dialog) diese Felder nicht kennt.
        if (policy.PdfFilePath is not null)
        {
            existing.PdfFilePath = policy.PdfFilePath;
            existing.PdfFileName = policy.PdfFileName;
        }

        await db.SaveChangesAsync();
        return existing;
    }

    public async Task<InsurancePolicy> UploadPolicyPdfAsync(int propertyId, InsuranceCategory category, IBrowserFile file, CancellationToken cancellationToken = default)
    {
        if (file.ContentType != "application/pdf" && !file.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"\"{file.Name}\": Bitte eine PDF-Datei hochladen.");
        }

        if (file.Size > MaxPdfSizeBytes)
        {
            throw new InvalidOperationException($"\"{file.Name}\" ist zu groß (max. {MaxPdfSizeBytes / 1024 / 1024} MB).");
        }

        var propertyDirectory = Path.Combine(_storageOptions.PoliciesDirectoryAbsolute, propertyId.ToString());
        Directory.CreateDirectory(propertyDirectory);

        var storedFileName = $"{category}_{Guid.NewGuid():N}.pdf";
        var absolutePath = Path.Combine(propertyDirectory, storedFileName);

        await using (var fileStream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write))
        await using (var browserStream = file.OpenReadStream(MaxPdfSizeBytes, cancellationToken))
        {
            await browserStream.CopyToAsync(fileStream, cancellationToken);
        }

        await using var db = await _contextFactory.CreateDbContextAsync();
        var policy = await db.InsurancePolicies
            .FirstOrDefaultAsync(i => i.PropertyId == propertyId && i.Category == category, cancellationToken);

        if (policy is null)
        {
            policy = new InsurancePolicy { PropertyId = propertyId, Category = category };
            db.InsurancePolicies.Add(policy);
        }

        policy.PdfFilePath = $"{StorageOptions.PoliciesRelativeRoot}/{propertyId}/{storedFileName}";
        policy.PdfFileName = file.Name;

        await db.SaveChangesAsync(cancellationToken);
        return policy;
    }

    public InsuranceBenchmarkResult CalculateBenchmark(Property property, InsuranceCategory category, decimal annualPremium)
    {
        var (min, max) = BenchmarkRanges[category];
        var unitCount = property.Units.Count;

        var result = new InsuranceBenchmarkResult
        {
            Category = category,
            AnnualPremium = annualPremium,
            UnitCount = unitCount,
            LivingAreaSqm = property.LivingAreaSqm,
            BenchmarkMinPerUnit = min,
            BenchmarkMaxPerUnit = max,
        };

        if (annualPremium <= 0 || unitCount == 0)
        {
            result.Status = InsuranceBenchmarkStatus.NichtErmittelbar;
            return result;
        }

        result.CostPerUnitAnnual = annualPremium / unitCount;
        result.CostPerSqmAnnual = property.LivingAreaSqm > 0 ? annualPremium / property.LivingAreaSqm : 0;

        result.Status = result.CostPerUnitAnnual switch
        {
            var cost when cost < min => InsuranceBenchmarkStatus.UngewoehnlichGuenstig,
            var cost when cost > max => InsuranceBenchmarkStatus.ZuTeuer,
            _ => InsuranceBenchmarkStatus.ImRahmen,
        };

        return result;
    }
}
