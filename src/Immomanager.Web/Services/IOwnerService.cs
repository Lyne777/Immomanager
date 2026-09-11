using Immomanager.Web.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace Immomanager.Web.Services;

public interface IOwnerService
{
    Task<List<Owner>> GetAllAsync();
    Task<Owner?> GetByIdAsync(int id);
    Task<Owner> CreateAsync(Owner owner);
    Task UpdateAsync(Owner owner);
    Task DeleteAsync(int id);
    Task<Owner> UploadLetterheadAsync(int ownerId, IBrowserFile file, CancellationToken cancellationToken = default);
    Task RemoveLetterheadAsync(int ownerId);
}
