using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public interface IUtilityStatementAnalysisService
{
    bool IsConfigured { get; }

    Task<UtilityStatementAnalysisResult> AnalyzeAsync(string statementText, CancellationToken cancellationToken = default);
}
