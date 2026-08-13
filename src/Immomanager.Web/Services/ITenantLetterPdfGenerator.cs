using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public interface ITenantLetterPdfGenerator
{
    Task<(string FileName, string Url)> GenerateAsync(
        int propertyId,
        int unitId,
        TenantLetterType letterType,
        string subject,
        string bodyText,
        string? senderName,
        string? senderAddress,
        CancellationToken cancellationToken = default);
}
