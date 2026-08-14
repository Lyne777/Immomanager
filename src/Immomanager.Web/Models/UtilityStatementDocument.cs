using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Eine von ggf. mehreren hochgeladenen PDFs zu einer Nebenkostenabrechnung (1:N an
/// UtilityStatement) - z. B. wenn die Hausverwaltung je Einheit eine eigene, personalisierte
/// Abrechnung ausstellt statt einer gemeinsamen Abrechnung fürs ganze Objekt.</summary>
public class UtilityStatementDocument
{
    public int Id { get; set; }

    public int UtilityStatementId { get; set; }

    [ForeignKey(nameof(UtilityStatementId))]
    public UtilityStatement? UtilityStatement { get; set; }

    [Required, StringLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string FileName { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
