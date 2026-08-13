using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Ein allgemeines Dokument zu einer Immobilie (z. B. Energieausweis, Grundbuchauszug) -
/// bewusst als offene, mehrfach befüllbare Ablage (nicht 1:1 wie InsurancePolicy), da z. B. ein
/// Energieausweis im Zeitverlauf erneuert wird und ältere Fassungen nicht zwingend gelöscht werden.</summary>
public class PropertyDocument
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property? Property { get; set; }

    public PropertyDocumentType DocumentType { get; set; }

    /// <summary>Freitext-Bezeichnung, insbesondere bei "Sonstiges" zur näheren Beschreibung.</summary>
    [StringLength(200)]
    public string? Title { get; set; }

    [Required, StringLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string FileName { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
