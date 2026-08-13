namespace Immomanager.Web.Models;

/// <summary>Eine Nachricht im Chat-Verlauf mit "Armin Asset" (nur für die UI-Anzeige - der volle
/// Anthropic-Nachrichtenverlauf inkl. Tool-Aufrufen wird separat im Agenten-Service gehalten).</summary>
public class ArminChatMessage
{
    public bool IsUser { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? DownloadFileName { get; set; }
    public string? DownloadUrl { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
