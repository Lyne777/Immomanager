namespace Immomanager.Web.Models;

/// <summary>Ergebnis einer abgeschlossenen Agenten-Runde (nach ggf. mehreren Tool-Aufrufen).</summary>
public record ArminAgentTurnResult(string ResponseText, string? DownloadFileName, string? DownloadUrl);
