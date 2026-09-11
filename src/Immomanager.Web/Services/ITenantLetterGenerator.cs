using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public interface ITenantLetterGenerator
{
    Task<(string FileName, string Url)> GenerateAsync(
        int propertyId,
        int unitId,
        TenantLetterType letterType,
        string subject,
        string bodyText,
        CancellationToken cancellationToken = default);
}
