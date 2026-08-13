using Immomanager.Web.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace Immomanager.Web.Services;

public interface IPropertyImageService
{
    Task<List<PropertyImage>> GetByPropertyIdAsync(int propertyId);
    Task<PropertyImage> UploadAsync(int propertyId, IBrowserFile file, CancellationToken cancellationToken = default);
    Task DeleteAsync(int imageId);
}
