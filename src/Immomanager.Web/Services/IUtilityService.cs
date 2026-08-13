using Immomanager.Web.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace Immomanager.Web.Services;

public interface IUtilityService
{
    Task<List<UtilityStatement>> GetStatementsAsync(int propertyId);

    Task<UtilityStatement?> GetStatementAsync(int propertyId, int year);

    /// <summary>Legt die Abrechnung für Immobilie+Jahr an oder aktualisiert die vorhandene
    /// (eindeutig je PropertyId+Year).</summary>
    Task<UtilityStatement> UpsertStatementAsync(UtilityStatement statement);

    Task DeleteStatementAsync(int statementId);

    Task<UtilityCostItem> CreateItemAsync(UtilityCostItem item);

    Task UpdateItemAsync(UtilityCostItem item);

    Task DeleteItemAsync(int itemId);

    Task<UtilityStatement> UploadStatementPdfAsync(int propertyId, int year, IBrowserFile file, CancellationToken cancellationToken = default);

    UtilityStatementKpi CalculateKpi(Property property, UtilityStatement statement);

    /// <summary>Immobilien aus der übergebenen Liste, für die im angegebenen Jahr noch keine
    /// Abrechnung vorliegt.</summary>
    Task<List<Property>> GetPropertiesMissingStatementAsync(IReadOnlyList<Property> properties, int year);

    /// <summary>Portfolioweiter Vergleich für das angegebene Jahr - enthält nur Immobilien, für die
    /// bereits eine Abrechnung vorliegt.</summary>
    Task<List<PortfolioUtilityComparisonRow>> GetPortfolioComparisonAsync(IReadOnlyList<Property> properties, int year);
}
