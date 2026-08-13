using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public interface IExposeAnalysisService
{
    /// <summary>Ob ein Anthropic API-Key hinterlegt ist. Vor jedem Analyse-Versuch im Frontend prüfen,
    /// um bei fehlender Konfiguration einen freundlichen Hinweis statt eines API-Fehlers zu zeigen.</summary>
    bool IsConfigured { get; }

    Task<ExposeAnalysisResult> AnalyzeAsync(string exposeText, CancellationToken cancellationToken = default);
}
