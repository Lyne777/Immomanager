using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Ein allgemeines Dokument zu einer Einheit (z. B. Grundriss, Übergabeprotokoll) - bewusst
/// als offene, mehrfach befüllbare Ablage analog zu <see cref="PropertyDocument"/>.</summary>
public class UnitDocument
{
    public int Id { get; set; }

    public int PropertyUnitId { get; set; }

    [ForeignKey(nameof(PropertyUnitId))]
    public PropertyUnit? PropertyUnit { get; set; }

    public UnitDocumentType DocumentType { get; set; }

    /// <summary>Freitext-Bezeichnung, insbesondere bei "Sonstiges" zur näheren Beschreibung.</summary>
    [StringLength(200)]
    public string? Title { get; set; }

    [Required, StringLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string FileName { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
