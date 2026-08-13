using Immomanager.Web.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace Immomanager.Web.Services;

/// <summary>Verwaltet allgemeine, frei erweiterbare Dokumente auf Objekt- und Einheiten-Ebene
/// (z. B. Energieausweis, Grundriss) - im Unterschied zu InsurancePolicy/UtilityStatement bewusst
/// als Mehrfach-Ablage je Typ (z. B. mehrere Grundriss-Seiten oder ein erneuerter Energieausweis).</summary>
public interface IDocumentService
{
    Task<List<PropertyDocument>> GetPropertyDocumentsAsync(int propertyId);

    Task<PropertyDocument> UploadPropertyDocumentAsync(
        int propertyId, PropertyDocumentType documentType, string? title, IBrowserFile file, CancellationToken cancellationToken = default);

    Task DeletePropertyDocumentAsync(int documentId);

    Task<List<UnitDocument>> GetUnitDocumentsAsync(int unitId);

    Task<UnitDocument> UploadUnitDocumentAsync(
        int unitId, UnitDocumentType documentType, string? title, IBrowserFile file, CancellationToken cancellationToken = default);

    Task DeleteUnitDocumentAsync(int documentId);

    /// <summary>Liefert die PropertyIds aus <paramref name="propertyIds"/>, für die mindestens ein
    /// Dokument des angegebenen Typs hinterlegt ist - Grundlage für den Dashboard-Fehlt-Check.</summary>
    Task<List<int>> GetPropertyIdsWithDocumentTypeAsync(IReadOnlyList<int> propertyIds, PropertyDocumentType documentType);

    /// <summary>Liefert die UnitIds aus <paramref name="unitIds"/>, für die mindestens ein Dokument
    /// des angegebenen Typs hinterlegt ist - Grundlage für den Dashboard-Fehlt-Check.</summary>
    Task<List<int>> GetUnitIdsWithDocumentTypeAsync(IReadOnlyList<int> unitIds, UnitDocumentType documentType);
}
