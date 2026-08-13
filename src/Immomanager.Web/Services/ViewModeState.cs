using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

/// <summary>Hält die aktuell gewählte Ansicht (Gesamtobjekt vs. Mein Anteil) für die Dauer der Session
/// und benachrichtigt Komponenten über Änderungen, damit Dashboard und Detailseiten synchron bleiben.</summary>
public class ViewModeState
{
    public ViewMode Current { get; private set; } = ViewMode.MyShare;

    public event Action? Changed;

    public void Set(ViewMode mode)
    {
        if (Current == mode)
        {
            return;
        }

        Current = mode;
        Changed?.Invoke();
    }
}
