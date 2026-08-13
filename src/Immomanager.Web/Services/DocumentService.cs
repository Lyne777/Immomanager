using Immomanager.Web.Data;
using Immomanager.Web.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace Immomanager.Web.Services;

public class DocumentService : IDocumentService
{
    public const long MaxFileSizeBytes = 20 * 1024 * 1024;

    public static readonly Dictionary<PropertyDocumentType, string> PropertyDocumentTypeLabels = new()
    {
        [PropertyDocumentType.Energieausweis] = "Energieausweis",
        [PropertyDocumentType.Grundbuchauszug] = "Grundbuchauszug",
        [PropertyDocumentType.Teilungserklaerung] = "Teilungserklärung",
        [PropertyDocumentType.Baugenehmigung] = "Baugenehmigung",
        [PropertyDocumentType.Sonstiges] = "Sonstiges",
    };

    public static readonly Dictionary<UnitDocumentType, string> UnitDocumentTypeLabels = new()
    {
        [UnitDocumentType.Grundriss] = "Grundriss",
        [UnitDocumentType.Uebergabeprotokoll] = "Übergabeprotokoll",
        [UnitDocumentType.Sonstiges] = "Sonstiges",
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/jpeg", "image/png", "image/webp",
    };

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly StorageOptions _storageOptions;

    public DocumentService(IDbContextFactory<ApplicationDbContext> contextFactory, StorageOptions storageOptions)
    {
        _contextFactory = contextFactory;
        _storageOptions = storageOptions;
    }

    public async Task<List<PropertyDocument>> GetPropertyDocumentsAsync(int propertyId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.PropertyDocuments
            .Where(d => d.PropertyId == propertyId)
            .AsNoTracking()
            .OrderByDescending(d => d.UploadedAtUtc)
            .ToListAsync();
    }

    public async Task<PropertyDocument> UploadPropertyDocumentAsync(
        int propertyId, PropertyDocumentType documentType, string? title, IBrowserFile file, CancellationToken cancellationToken = default)
    {
        ValidateFile(file);

        var propertyDirectory = Path.Combine(_storageOptions.DocumentsDirectoryAbsolute, "properties", propertyId.ToString());
        Directory.CreateDirectory(propertyDirectory);

        var extension = Path.GetExtension(file.Name);
        var storedFileName = $"{documentType}_{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(propertyDirectory, storedFileName);

        await using (var fileStream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write))
        await using (var browserStream = file.OpenReadStream(MaxFileSizeBytes, cancellationToken))
        {
            await browserStream.CopyToAsync(fileStream, cancellationToken);
        }

        var document = new PropertyDocument
        {
            PropertyId = propertyId,
            DocumentType = documentType,
            Title = string.IsNullOrWhiteSpace(title) ? null : title,
            FilePath = $"{StorageOptions.DocumentsRelativeRoot}/properties/{propertyId}/{storedFileName}",
            FileName = file.Name,
        };

        await using var db = await _contextFactory.CreateDbContextAsync();
        db.PropertyDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task DeletePropertyDocumentAsync(int documentId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var document = await db.PropertyDocuments.FindAsync(documentId);
        if (document is null)
        {
            return;
        }

        db.PropertyDocuments.Remove(document);
        await db.SaveChangesAsync();
        DeleteFileIfExists(document.FilePath);
    }

    public async Task<List<UnitDocument>> GetUnitDocumentsAsync(int unitId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.UnitDocuments
            .Where(d => d.PropertyUnitId == unitId)
            .AsNoTracking()
            .OrderByDescending(d => d.UploadedAtUtc)
            .ToListAsync();
    }

    public async Task<UnitDocument> UploadUnitDocumentAsync(
        int unitId, UnitDocumentType documentType, string? title, IBrowserFile file, CancellationToken cancellationToken = default)
    {
        ValidateFile(file);

        var unitDirectory = Path.Combine(_storageOptions.DocumentsDirectoryAbsolute, "units", unitId.ToString());
        Directory.CreateDirectory(unitDirectory);

        var extension = Path.GetExtension(file.Name);
        var storedFileName = $"{documentType}_{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(unitDirectory, storedFileName);

        await using (var fileStream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write))
        await using (var browserStream = file.OpenReadStream(MaxFileSizeBytes, cancellationToken))
        {
            await browserStream.CopyToAsync(fileStream, cancellationToken);
        }

        var document = new UnitDocument
        {
            PropertyUnitId = unitId,
            DocumentType = documentType,
            Title = string.IsNullOrWhiteSpace(title) ? null : title,
            FilePath = $"{StorageOptions.DocumentsRelativeRoot}/units/{unitId}/{storedFileName}",
            FileName = file.Name,
        };

        await using var db = await _contextFactory.CreateDbContextAsync();
        db.UnitDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task DeleteUnitDocumentAsync(int documentId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var document = await db.UnitDocuments.FindAsync(documentId);
        if (document is null)
        {
            return;
        }

        db.UnitDocuments.Remove(document);
        await db.SaveChangesAsync();
        DeleteFileIfExists(document.FilePath);
    }

    public async Task<List<int>> GetPropertyIdsWithDocumentTypeAsync(IReadOnlyList<int> propertyIds, PropertyDocumentType documentType)
    {
        if (propertyIds.Count == 0)
        {
            return new List<int>();
        }

        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.PropertyDocuments
            .Where(d => d.DocumentType == documentType && propertyIds.Contains(d.PropertyId))
            .Select(d => d.PropertyId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<List<int>> GetUnitIdsWithDocumentTypeAsync(IReadOnlyList<int> unitIds, UnitDocumentType documentType)
    {
        if (unitIds.Count == 0)
        {
            return new List<int>();
        }

        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.UnitDocuments
            .Where(d => d.DocumentType == documentType && unitIds.Contains(d.PropertyUnitId))
            .Select(d => d.PropertyUnitId)
            .Distinct()
            .ToListAsync();
    }

    private static void ValidateFile(IBrowserFile file)
    {
        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            throw new InvalidOperationException(
                $"\"{file.Name}\": Dateityp \"{file.ContentType}\" wird nicht unterstützt (nur PDF, JPG, PNG, WEBP).");
        }

        if (file.Size > MaxFileSizeBytes)
        {
            throw new InvalidOperationException($"\"{file.Name}\" ist zu groß (max. {MaxFileSizeBytes / 1024 / 1024} MB).");
        }
    }

    private void DeleteFileIfExists(string relativePath)
    {
        var absolutePath = Path.Combine(_storageOptions.DataDirectoryAbsolute, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
    }
}
