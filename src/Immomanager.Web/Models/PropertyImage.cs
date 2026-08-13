using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Ein Foto zu einer Immobilie. Die Datei liegt im Dateisystem unter dem konfigurierten
/// Datenverzeichnis; hier wird nur der relative Pfad dazu gespeichert (z. B. "uploads/3/&lt;guid&gt;.jpg").</summary>
public class PropertyImage
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property? Property { get; set; }

    public string RelativePath { get; set; } = string.Empty;

    /// <summary>Ursprünglicher Dateiname zur Anzeige, nicht der auf der Platte verwendete Dateiname.</summary>
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
