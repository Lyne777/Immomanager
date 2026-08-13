using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Die Vertragsfakten einer Versicherung zu einer Immobilie - je Immobilie und Kategorie
/// (Gebäude/Haftpflicht) genau eine aktuelle Police. Die Prüf-Checkliste hängt bewusst NICHT an
/// dieser Entität, sondern direkt an <see cref="Property"/> (<see cref="InsuranceCheckItem"/>),
/// damit sie unabhängig von einer konkreten Police pro Objekt gepflegt werden kann.</summary>
public class InsurancePolicy
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property? Property { get; set; }

    public InsuranceCategory Category { get; set; }

    [StringLength(200)]
    public string? Provider { get; set; }

    [StringLength(100)]
    public string? PolicyNumber { get; set; }

    [Range(0, double.MaxValue)]
    public decimal AnnualPremium { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? ExpirationDate { get; set; }

    /// <summary>Relativer Pfad zur hochgeladenen Policen-PDF unterhalb von
    /// {DataDirectory}/policies/, analog zu PropertyImage.RelativePath.</summary>
    [StringLength(500)]
    public string? PdfFilePath { get; set; }

    [StringLength(200)]
    public string? PdfFileName { get; set; }
}
