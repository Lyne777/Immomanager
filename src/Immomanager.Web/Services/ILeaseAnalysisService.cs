using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public interface ILeaseAnalysisService
{
    bool IsConfigured { get; }

    Task<LeaseAnalysisResult> AnalyzeAsync(string leaseText, CancellationToken cancellationToken = default);
}
