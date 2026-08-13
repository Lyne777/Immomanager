using Immomanager.Web.Models;
using MudBlazor;

namespace Immomanager.Web.Services;

/// <summary>Bündelt alle über die App verstreuten Vollständigkeits-Prüfungen zu einer zentralen,
/// klickbaren Aufgabenliste fürs Dashboard. Neue Prüfungen (z. B. für ein künftiges Modul) werden
/// hier als weiterer Block in <see cref="GetOpenItemsAsync"/> ergänzt, statt an verstreuten Stellen
/// in der UI eigene Banner zu bauen.</summary>
public class DashboardTodoService : IDashboardTodoService
{
    private readonly IPropertyService _propertyService;
    private readonly RentTargetAnalyticsService _rentTargetAnalytics;
    private readonly IUtilityService _utilityService;
    private readonly IDocumentService _documentService;

    public DashboardTodoService(
        IPropertyService propertyService,
        RentTargetAnalyticsService rentTargetAnalytics,
        IUtilityService utilityService,
        IDocumentService documentService)
    {
        _propertyService = propertyService;
        _rentTargetAnalytics = rentTargetAnalytics;
        _utilityService = utilityService;
        _documentService = documentService;
    }

    public async Task<List<DashboardTodoItem>> GetOpenItemsAsync()
    {
        var properties = await _propertyService.GetAllAsync();
        var items = new List<DashboardTodoItem>();

        var (currentYear, currentQuarter) = RentTargetAnalyticsService.GetQuarterOf(DateTime.Today);
        foreach (var property in properties.Where(_rentTargetAnalytics.IsMissingCurrentQuarterTarget))
        {
            items.Add(new DashboardTodoItem
            {
                Title = "Soll-Miete fehlt",
                Description = $"{property.Name}: Q{currentQuarter} {currentYear}",
                Url = $"properties/{property.Id}?tab=sollmiete",
                Icon = Icons.Material.Filled.Rule,
            });
        }

        var utilityYear = DateTime.Today.Year - 1;
        var propertiesMissingUtilityStatement = await _utilityService.GetPropertiesMissingStatementAsync(properties, utilityYear);
        foreach (var property in propertiesMissingUtilityStatement)
        {
            items.Add(new DashboardTodoItem
            {
                Title = "Nebenkostenabrechnung fehlt",
                Description = $"{property.Name}: Abrechnungsjahr {utilityYear}",
                Url = $"properties/{property.Id}?tab=nebenkosten",
                Icon = Icons.Material.Filled.Receipt,
            });
        }

        var propertyIds = properties.Select(p => p.Id).ToList();
        var propertyIdsWithEnergieausweis = await _documentService.GetPropertyIdsWithDocumentTypeAsync(propertyIds, PropertyDocumentType.Energieausweis);
        foreach (var property in properties.Where(p => !propertyIdsWithEnergieausweis.Contains(p.Id)))
        {
            items.Add(new DashboardTodoItem
            {
                Title = "Energieausweis fehlt",
                Description = property.Name,
                Url = $"properties/{property.Id}?tab=dokumente",
                Icon = Icons.Material.Filled.Description,
            });
        }

        var allUnits = properties.SelectMany(p => p.Units.Select(u => (Property: p, Unit: u))).ToList();
        var unitIds = allUnits.Select(x => x.Unit.Id).ToList();
        var unitIdsWithGrundriss = await _documentService.GetUnitIdsWithDocumentTypeAsync(unitIds, UnitDocumentType.Grundriss);
        foreach (var (property, unit) in allUnits.Where(x => !unitIdsWithGrundriss.Contains(x.Unit.Id)))
        {
            items.Add(new DashboardTodoItem
            {
                Title = "Grundriss fehlt",
                Description = $"{property.Name}: {unit.Label}",
                Url = $"properties/{property.Id}/units/{unit.Id}",
                Icon = Icons.Material.Filled.Architecture,
            });
        }

        return items;
    }
}
