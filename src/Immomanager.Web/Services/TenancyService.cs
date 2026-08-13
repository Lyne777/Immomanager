using Immomanager.Web.Data;
using Immomanager.Web.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace Immomanager.Web.Services;

public class TenancyService : ITenancyService
{
    public const long MaxPdfSizeBytes = 20 * 1024 * 1024;

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly StorageOptions _storageOptions;

    public TenancyService(IDbContextFactory<ApplicationDbContext> contextFactory, StorageOptions storageOptions)
    {
        _contextFactory = contextFactory;
        _storageOptions = storageOptions;
    }

    public async Task<List<Tenancy>> GetTenanciesAsync(int unitId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Tenancies
            .Where(t => t.PropertyUnitId == unitId)
            .AsNoTracking()
            .OrderByDescending(t => t.MoveInDate)
            .ToListAsync();
    }

    public async Task<Tenancy?> GetByIdAsync(int tenancyId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Tenancies.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenancyId);
    }

    public async Task<Tenancy> CreateAsync(Tenancy tenancy)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        db.Tenancies.Add(tenancy);
        await db.SaveChangesAsync();
        return tenancy;
    }

    public async Task UpdateAsync(Tenancy tenancy)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        db.Tenancies.Update(tenancy);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int tenancyId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var tenancy = await db.Tenancies.FindAsync(tenancyId);
        if (tenancy is not null)
        {
            db.Tenancies.Remove(tenancy);
            await db.SaveChangesAsync();
        }
    }

    public async Task<Tenancy> UploadLeasePdfAsync(int unitId, IBrowserFile file, CancellationToken cancellationToken = default)
    {
        if (file.ContentType != "application/pdf" && !file.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"\"{file.Name}\": Bitte eine PDF-Datei hochladen.");
        }

        if (file.Size > MaxPdfSizeBytes)
        {
            throw new InvalidOperationException($"\"{file.Name}\" ist zu groß (max. {MaxPdfSizeBytes / 1024 / 1024} MB).");
        }

        var unitDirectory = Path.Combine(_storageOptions.LeasesDirectoryAbsolute, unitId.ToString());
        Directory.CreateDirectory(unitDirectory);

        var storedFileName = $"{Guid.NewGuid():N}.pdf";
        var absolutePath = Path.Combine(unitDirectory, storedFileName);

        await using (var fileStream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write))
        await using (var browserStream = file.OpenReadStream(MaxPdfSizeBytes, cancellationToken))
        {
            await browserStream.CopyToAsync(fileStream, cancellationToken);
        }

        // Ein neuer Mietvertrags-Upload steht für ein neues Mietverhältnis - anders als bei Police/
        // Abrechnung (eindeutig je Kategorie/Jahr) gibt es hier keinen natürlichen Schlüssel, über den
        // sich ein Upload einem VORHANDENEN Mietverhältnis eindeutig zuordnen ließe. Die Platzhalter-
        // Mieterdaten macht Armin Asset (analyze_lease_pdf) oder die manuelle Bearbeitung vollständig.
        var tenancy = new Tenancy
        {
            PropertyUnitId = unitId,
            TenantName = "(noch nicht ausgefüllt - Mietvertrag wartet auf Auswertung)",
            PdfFilePath = $"{StorageOptions.LeasesRelativeRoot}/{unitId}/{storedFileName}",
            PdfFileName = file.Name,
        };

        await using var db = await _contextFactory.CreateDbContextAsync();
        db.Tenancies.Add(tenancy);
        await db.SaveChangesAsync(cancellationToken);
        return tenancy;
    }
}
