using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public record BringList(string Uuid, string Name);

/// <summary>Sendet Einkaufslisten an die "Bring!"-App über deren inoffizielle, nicht dokumentierte
/// REST-API. Rein optionale Zusatzfunktion - kann sich jederzeit ändern, da nicht offiziell
/// unterstützt; der PDF-Export bleibt der zuverlässige Weg.</summary>
public interface IBringService
{
    /// <summary>Meldet sich mit den aktuell gespeicherten Zugangsdaten an und liefert die Listen des
    /// Kontos - zur Auswahl der Zielliste in den Küche-Einstellungen. Wirft bei fehlenden/ungültigen
    /// Zugangsdaten oder einem API-Fehler eine <see cref="InvalidOperationException"/> mit
    /// nutzerfreundlicher Meldung.</summary>
    Task<List<BringList>> GetListsAsync(CancellationToken cancellationToken = default);

    /// <summary>Sendet die übergebenen Positionen an die in den Einstellungen hinterlegte Zielliste.</summary>
    Task SendItemsAsync(IEnumerable<ShoppingListItem> items, CancellationToken cancellationToken = default);
}
