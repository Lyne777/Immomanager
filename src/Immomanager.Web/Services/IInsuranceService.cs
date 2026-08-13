using Immomanager.Web.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace Immomanager.Web.Services;

public interface IInsuranceService
{
    /// <summary>Liefert die (bei Bedarf zuvor aus dem Katalog nachgezogenen) Checklisten-Positionen
    /// einer Immobilie, sortiert nach Kategorie und fester Reihenfolge.</summary>
    Task<List<InsuranceCheckItem>> GetCheckItemsAsync(int propertyId);

    Task UpdateCheckItemAsync(InsuranceCheckItem item);

    Task<List<InsurancePolicy>> GetPoliciesAsync(int propertyId);

    Task<InsurancePolicy?> GetPolicyAsync(int propertyId, InsuranceCategory category);

    /// <summary>Legt die Police für Immobilie+Kategorie an oder aktualisiert die vorhandene
    /// (eindeutig je PropertyId+Category).</summary>
    Task<InsurancePolicy> UpsertPolicyAsync(InsurancePolicy policy);

    Task<InsurancePolicy> UploadPolicyPdfAsync(int propertyId, InsuranceCategory category, IBrowserFile file, CancellationToken cancellationToken = default);

    InsuranceBenchmarkResult CalculateBenchmark(Property property, InsuranceCategory category, decimal annualPremium);
}
