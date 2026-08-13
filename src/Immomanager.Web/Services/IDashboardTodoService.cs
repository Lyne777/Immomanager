using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

/// <summary>Bündelt alle über die App verstreuten Vollständigkeits-Prüfungen (Soll-Miete,
/// Nebenkostenabrechnung, Dokumente) zu einer zentralen, klickbaren Aufgabenliste fürs Dashboard.</summary>
public interface IDashboardTodoService
{
    Task<List<DashboardTodoItem>> GetOpenItemsAsync();
}
