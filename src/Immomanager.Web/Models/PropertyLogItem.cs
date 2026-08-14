namespace Immomanager.Web.Models;

public enum PropertyLogItemSource
{
    Manual,
    Renovation,
}

/// <summary>Ein Eintrag in der zusammengeführten, chronologischen Logbuch-Ansicht - entweder aus einem
/// manuellen <see cref="PropertyLogEntry"/> oder abgeleitet aus einem <see cref="RenovationProject"/>.
/// Siehe <see cref="PropertyLogService.GetCombinedLogAsync"/>.</summary>
public class PropertyLogItem
{
    public PropertyLogItemSource Source { get; set; }

    /// <summary>Für die chronologische Sortierung aus <see cref="DateLabel"/> extrahiertes Jahr
    /// (0, falls kein Jahr erkennbar war - solche Einträge landen dann am Anfang der Liste).</summary>
    public int SortYear { get; set; }

    public string DateLabel { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Null = betrifft das ganze Objekt.</summary>
    public string? UnitLabel { get; set; }

    /// <summary>Nur bei <see cref="PropertyLogItemSource.Renovation"/> gesetzt (Ist-Kosten).</summary>
    public decimal? Cost { get; set; }

    public int? ManualEntryId { get; set; }

    public int? RenovationProjectId { get; set; }
}
