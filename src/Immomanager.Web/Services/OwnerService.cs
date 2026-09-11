using Immomanager.Web.Data;
using Immomanager.Web.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace Immomanager.Web.Services;

/// <summary>Verwaltet Eigentümer (Person/Gesellschaft) inkl. optionalem Word-Briefkopf, der als Vorlage
/// für von Armin Asset generierte Mieterschreiben dient.</summary>
public class OwnerService : IOwnerService
{
    public const long MaxLetterheadSizeBytes = 20 * 1024 * 1024;
    private const string LetterheadContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly StorageOptions _storageOptions;

    public OwnerService(IDbContextFactory<ApplicationDbContext> contextFactory, StorageOptions storageOptions)
    {
        _contextFactory = contextFactory;
        _storageOptions = storageOptions;
    }

    public async Task<List<Owner>> GetAllAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Owners.AsNoTracking().OrderBy(o => o.Name).ToListAsync();
    }

    public async Task<Owner?> GetByIdAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Owners.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<Owner> CreateAsync(Owner owner)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        db.Owners.Add(owner);
        await db.SaveChangesAsync();
        return owner;
    }

    public async Task UpdateAsync(Owner owner)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var existing = await db.Owners.FirstOrDefaultAsync(o => o.Id == owner.Id)
            ?? throw new InvalidOperationException($"Eigentümer mit Id {owner.Id} wurde nicht gefunden.");

        existing.Name = owner.Name;
        existing.Address = owner.Address;
        // LetterheadFilePath/LetterheadFileName werden separat über UploadLetterheadAsync gepflegt.
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var owner = await db.Owners.FindAsync(id);
        if (owner is null)
        {
            return;
        }

        db.Owners.Remove(owner);
        await db.SaveChangesAsync();

        DeleteLetterheadFile(owner.LetterheadFilePath);
    }

    public async Task<Owner> UploadLetterheadAsync(int ownerId, IBrowserFile file, CancellationToken cancellationToken = default)
    {
        if (file.ContentType != LetterheadContentType && !file.Name.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"\"{file.Name}\": Bitte eine Word-Datei (.docx) hochladen.");
        }

        if (file.Size > MaxLetterheadSizeBytes)
        {
            throw new InvalidOperationException($"\"{file.Name}\" ist zu groß (max. {MaxLetterheadSizeBytes / 1024 / 1024} MB).");
        }

        await using var db = await _contextFactory.CreateDbContextAsync();
        var owner = await db.Owners.FirstOrDefaultAsync(o => o.Id == ownerId, cancellationToken)
            ?? throw new InvalidOperationException($"Eigentümer mit Id {ownerId} wurde nicht gefunden.");

        Directory.CreateDirectory(_storageOptions.LetterheadsDirectoryAbsolute);

        var storedFileName = $"{ownerId}_{Guid.NewGuid():N}.docx";
        var absolutePath = Path.Combine(_storageOptions.LetterheadsDirectoryAbsolute, storedFileName);

        await using (var fileStream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write))
        await using (var browserStream = file.OpenReadStream(MaxLetterheadSizeBytes, cancellationToken))
        {
            await browserStream.CopyToAsync(fileStream, cancellationToken);
        }

        var previousFilePath = owner.LetterheadFilePath;
        owner.LetterheadFilePath = $"{StorageOptions.LetterheadsRelativeRoot}/{storedFileName}";
        owner.LetterheadFileName = file.Name;
        await db.SaveChangesAsync(cancellationToken);

        DeleteLetterheadFile(previousFilePath);
        return owner;
    }

    public async Task RemoveLetterheadAsync(int ownerId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var owner = await db.Owners.FirstOrDefaultAsync(o => o.Id == ownerId)
            ?? throw new InvalidOperationException($"Eigentümer mit Id {ownerId} wurde nicht gefunden.");

        var previousFilePath = owner.LetterheadFilePath;
        owner.LetterheadFilePath = null;
        owner.LetterheadFileName = null;
        await db.SaveChangesAsync();

        DeleteLetterheadFile(previousFilePath);
    }

    private void DeleteLetterheadFile(string? relativeFilePath)
    {
        if (string.IsNullOrEmpty(relativeFilePath))
        {
            return;
        }

        var absolutePath = Path.Combine(_storageOptions.DataDirectoryAbsolute, relativeFilePath);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
    }
}
