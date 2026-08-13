using Immomanager.Web.Data;
using Immomanager.Web.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace Immomanager.Web.Services;

public class PropertyImageService : IPropertyImageService
{
    public const long MaxFileSizeBytes = 15 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif",
    };

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly StorageOptions _storageOptions;

    public PropertyImageService(IDbContextFactory<ApplicationDbContext> contextFactory, StorageOptions storageOptions)
    {
        _contextFactory = contextFactory;
        _storageOptions = storageOptions;
    }

    public async Task<List<PropertyImage>> GetByPropertyIdAsync(int propertyId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.PropertyImages
            .Where(i => i.PropertyId == propertyId)
            .OrderBy(i => i.UploadedAtUtc)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<PropertyImage> UploadAsync(int propertyId, IBrowserFile file, CancellationToken cancellationToken = default)
    {
        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            throw new InvalidOperationException(
                $"\"{file.Name}\": Dateityp \"{file.ContentType}\" wird nicht unterstützt (nur JPG, PNG, WEBP, GIF).");
        }

        if (file.Size > MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"\"{file.Name}\" ist zu groß (max. {MaxFileSizeBytes / 1024 / 1024} MB).");
        }

        var propertyDirectory = Path.Combine(_storageOptions.UploadsDirectoryAbsolute, propertyId.ToString());
        Directory.CreateDirectory(propertyDirectory);

        var extension = Path.GetExtension(file.Name);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(propertyDirectory, storedFileName);

        await using (var fileStream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write))
        await using (var browserStream = file.OpenReadStream(MaxFileSizeBytes, cancellationToken))
        {
            await browserStream.CopyToAsync(fileStream, cancellationToken);
        }

        var image = new PropertyImage
        {
            PropertyId = propertyId,
            RelativePath = $"{StorageOptions.UploadsRelativeRoot}/{propertyId}/{storedFileName}",
            FileName = file.Name,
            ContentType = file.ContentType,
            FileSizeBytes = file.Size,
            UploadedAtUtc = DateTime.UtcNow,
        };

        await using var db = await _contextFactory.CreateDbContextAsync();
        db.PropertyImages.Add(image);
        await db.SaveChangesAsync(cancellationToken);
        return image;
    }

    public async Task DeleteAsync(int imageId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var image = await db.PropertyImages.FindAsync(imageId);
        if (image is null)
        {
            return;
        }

        db.PropertyImages.Remove(image);
        await db.SaveChangesAsync();

        var absolutePath = Path.Combine(_storageOptions.DataDirectoryAbsolute, image.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
    }
}
