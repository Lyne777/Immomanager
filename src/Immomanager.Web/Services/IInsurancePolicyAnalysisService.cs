using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public interface IInsurancePolicyAnalysisService
{
    bool IsConfigured { get; }

    /// <summary>Analysiert den extrahierten Text einer Versicherungspolice und gleicht ihn mit den
    /// Prüfpunkten der jeweiligen Kategorie ab (<see cref="InsuranceCheckCatalog"/>).</summary>
    Task<InsurancePolicyAnalysisResult> AnalyzeAsync(string policyText, InsuranceCategory category, CancellationToken cancellationToken = default);
}
