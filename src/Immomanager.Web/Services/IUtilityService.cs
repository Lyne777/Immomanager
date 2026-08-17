using Immomanager.Web.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace Immomanager.Web.Services;

public interface IUtilityService
{
    /// <summary>Alle Abrechnungen einer Immobilie über alle Jahre - sowohl Ganzes-Objekt- (PropertyUnitId
    /// = null) als auch Einheiten-Abrechnungen. Grundlage für die Aggregat-KPIs und den
    /// Portfolio-Vergleich.</summary>
    Task<List<UtilityStatement>> GetStatementsForPropertyAsync(int propertyId);

    /// <summary>Alle Abrechnungen einer einzelnen Einheit über alle Jahre - die "Historie" für die
    /// Einheiten-Detailseite.</summary>
    Task<List<UtilityStatement>> GetStatementsForUnitAsync(int propertyUnitId);

    Task<UtilityStatement?> GetStatementAsync(int propertyId, int? propertyUnitId, int year);

    /// <summary>Legt die Abrechnung für Immobilie(+Einheit)+Jahr an oder aktualisiert die vorhandene
    /// (eindeutig je PropertyId+PropertyUnitId+Year).</summary>
    Task<UtilityStatement> UpsertStatementAsync(UtilityStatement statement);

    Task DeleteStatementAsync(int statementId);

    Task<UtilityCostItem> CreateItemAsync(UtilityCostItem item);

    Task UpdateItemAsync(UtilityCostItem item);

    Task DeleteItemAsync(int itemId);

    Task<UtilityStatement> UploadStatementPdfAsync(int propertyId, int? propertyUnitId, int year, IBrowserFile file, CancellationToken cancellationToken = default);

    Task DeleteStatementDocumentAsync(int documentId);

    /// <summary>"periodMonths": wie viele Monate "totalCosts" tatsächlich abdeckt (Standard 12) -
    /// die €/m²- und €/Einheit-Werte werden intern darauf hochgerechnet, damit z. B. eine
    /// Teilzeitraum-Abrechnung im Kaufjahr nicht als voller Jahreswert missverstanden wird.</summary>
    UtilityStatementKpi CalculateKpi(int year, decimal totalCosts, decimal periodMonths, decimal areaSqm, int unitCount);

    /// <summary>Objektweite Kennzahlen für ein Abrechnungsjahr - die Summe aus der Ganzes-Objekt-
    /// Abrechnung (falls vorhanden) und allen Einheiten-Abrechnungen dieses Jahres. Ist automatisch
    /// vollständig, sobald für jede relevante Einheit eine Abrechnung vorliegt; bis dahin ein
    /// ehrlicher Teil-Stand statt eines künstlich "vollständig" wirkenden Werts.</summary>
    Task<UtilityStatementKpi> CalculatePropertyKpiAsync(Property property, int year);

    /// <summary>Einheiten (Property+Unit), für die im angegebenen Jahr noch keine Abrechnung vorliegt -
    /// beschränkt auf Einheiten mit <see cref="PropertyUnit.CountsTowardRentTarget"/> = true, da z. B.
    /// Garagen/Stellplätze in aller Regel keine eigene Abrechnung bekommen.</summary>
    Task<List<(Property Property, PropertyUnit Unit)>> GetUnitsMissingStatementAsync(IReadOnlyList<Property> properties, int year);

    /// <summary>Portfolioweiter Vergleich für das angegebene Jahr - eine Zeile je Immobilie mit
    /// mindestens einer Abrechnung, deren Gesamtkosten aus allen Abrechnungen (Objekt + Einheiten)
    /// dieses Jahres summiert werden.</summary>
    Task<List<PortfolioUtilityComparisonRow>> GetPortfolioComparisonAsync(IReadOnlyList<Property> properties, int year);
}
