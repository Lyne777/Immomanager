using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Eine einzelne Position der Versicherungs-Prüf-Checkliste einer Immobilie. Hängt bewusst
/// direkt an <see cref="Property"/> (nicht an einer <see cref="InsurancePolicy"/>), damit die
/// Checkliste unabhängig von einer konkreten Police gepflegt werden kann. Wird beim Anlegen einer
/// Immobilie aus einem festen Katalog (<see cref="Services.InsuranceCheckCatalog"/>) automatisch
/// befüllt; "Key" identifiziert die Position stabil (auch für den Abgleich durch Armin Asset),
/// unabhängig vom - ggf. später redigierten - Anzeigetext in "Title".</summary>
public class InsuranceCheckItem
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property? Property { get; set; }

    public InsuranceCategory Category { get; set; }

    /// <summary>Stabiler technischer Schlüssel aus dem Katalog, z. B. "geb.elementarschutz".</summary>
    [Required, StringLength(100)]
    public string Key { get; set; } = string.Empty;

    /// <summary>Optionale Untergruppierung innerhalb der Kategorie, z. B. "WEG-Prüfung" oder
    /// "Klauseln &amp; Kleingedrucktes". Null für eigenständige (nicht gruppierte) Positionen.</summary>
    [StringLength(200)]
    public string? GroupLabel { get; set; }

    [Required, StringLength(500)]
    public string Title { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>null = noch nicht geprüft, true = abgedeckt, false = Lücke/nicht abgedeckt.</summary>
    public bool? IsCovered { get; set; }

    public string? Notes { get; set; }
}
