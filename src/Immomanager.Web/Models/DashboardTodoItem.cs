namespace Immomanager.Web.Models;

/// <summary>Ein einzelner offener Punkt im zentralen Dashboard-Aufgaben-Widget, z. B. ein fehlender
/// Soll-Mietwert oder ein fehlendes Dokument. <see cref="Url"/> führt direkt zur Stelle, an der der
/// Punkt behoben werden kann.</summary>
public class DashboardTodoItem
{
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Url { get; set; }
    public required string Icon { get; set; }
}
