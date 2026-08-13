using Anthropic.Models.Messages;
using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public interface IArminAssetAgentService
{
    /// <summary>Ob ein Anthropic API-Key hinterlegt ist.</summary>
    bool IsConfigured { get; }

    /// <summary>Hängt die Nutzer-Nachricht an den (mutierbaren) Gesprächsverlauf an, führt die
    /// Agenten-/Tool-Schleife aus und liefert die finale Textantwort inkl. optionalem Datei-Download.
    /// <paramref name="onStatusUpdate"/> wird bei jedem Tool-Aufruf für die Live-Anzeige im Chat aufgerufen.</summary>
    Task<ArminAgentTurnResult> SendMessageAsync(
        List<MessageParam> conversation,
        string userMessage,
        Func<string, Task> onStatusUpdate,
        CancellationToken cancellationToken = default);
}
