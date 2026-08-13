using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

/// <summary>Wertet abgeschlossene Renovierungsprojekte aus, um historische €/Einheit-Erfahrungswerte
/// je Gewerk und je Projektkategorie zu bilden, und liefert darauf basierende Kostenschätzungen
/// für neue Vorhaben (Renovierungs-Rechner).</summary>
public class RenovationAnalyticsService
{
    private readonly IRenovationService _renovationService;

    public RenovationAnalyticsService(IRenovationService renovationService)
    {
        _renovationService = renovationService;
    }

    public async Task<List<TradeLearningStat>> GetTradeLearningsAsync()
    {
        var completedProjects = (await _renovationService.GetAllProjectsAsync())
            .Where(p => p.Status == RenovationStatus.Abgeschlossen);

        var lineItems = completedProjects.SelectMany(p => p.LineItems).ToList();

        return lineItems
            .GroupBy(li => li.Trade)
            .Select(g => BuildTradeStat(g.Key, g.ToList()))
            .OrderByDescending(s => s.SampleCount)
            .ToList();
    }

    public async Task<List<CategoryLearningStat>> GetCategoryLearningsAsync()
    {
        var completedProjects = (await _renovationService.GetAllProjectsAsync())
            .Where(p => p.Status == RenovationStatus.Abgeschlossen && p.AreaSqm > 0)
            .ToList();

        return completedProjects
            .GroupBy(p => p.Category)
            .Select(g => BuildCategoryStat(g.Key, g.ToList()))
            .OrderByDescending(s => s.SampleCount)
            .ToList();
    }

    public List<ProjectTradeBreakdown> GetProjectTradeBreakdown(RenovationProject project)
    {
        return project.LineItems
            .GroupBy(li => li.Trade)
            .Select(g => new ProjectTradeBreakdown
            {
                Trade = g.Key,
                DominantUnit = MostFrequentUnit(g),
                Quantity = g.Sum(li => li.Quantity),
                MaterialCost = g.Sum(li => li.MaterialCost),
                LaborCost = g.Sum(li => li.LaborCost),
            })
            .OrderByDescending(b => b.TotalCost)
            .ToList();
    }

    public async Task<RenovationEstimate> EstimateByTradeAsync(RenovationTrade trade, decimal quantity)
    {
        var stats = await GetTradeLearningsAsync();
        var stat = stats.FirstOrDefault(s => s.Trade == trade);

        return new RenovationEstimate
        {
            Basis = TradeDisplayNames.GetValueOrDefault(trade, trade.ToString()),
            Quantity = quantity,
            Unit = stat?.DominantUnit ?? "m²",
            AverageCostPerUnit = stat?.AverageCostPerUnit ?? 0,
            MinCostPerUnit = stat?.MinCostPerUnit ?? 0,
            MaxCostPerUnit = stat?.MaxCostPerUnit ?? 0,
            SampleCount = stat?.SampleCount ?? 0,
        };
    }

    public async Task<RenovationEstimate> EstimateByCategoryAsync(RenovationCategory category, decimal areaSqm)
    {
        var stats = await GetCategoryLearningsAsync();
        var stat = stats.FirstOrDefault(s => s.Category == category);

        return new RenovationEstimate
        {
            Basis = CategoryDisplayNames.GetValueOrDefault(category, category.ToString()),
            Quantity = areaSqm,
            Unit = "m²",
            AverageCostPerUnit = stat?.AverageCostPerSqm ?? 0,
            MinCostPerUnit = stat?.MinCostPerSqm ?? 0,
            MaxCostPerUnit = stat?.MaxCostPerSqm ?? 0,
            SampleCount = stat?.SampleCount ?? 0,
        };
    }

    private static TradeLearningStat BuildTradeStat(RenovationTrade trade, List<RenovationLineItem> items)
    {
        var itemsWithQuantity = items.Where(li => li.Quantity > 0).ToList();
        var unitRates = itemsWithQuantity.Select(li => li.CostPerUnit).ToList();

        return new TradeLearningStat
        {
            Trade = trade,
            SampleCount = items.Count,
            DominantUnit = MostFrequentUnit(items),
            TotalQuantity = items.Sum(li => li.Quantity),
            TotalMaterialCost = items.Sum(li => li.MaterialCost),
            TotalLaborCost = items.Sum(li => li.LaborCost),
            TotalCost = items.Sum(li => li.TotalCost),
            MinCostPerUnit = unitRates.Count > 0 ? unitRates.Min() : 0,
            MaxCostPerUnit = unitRates.Count > 0 ? unitRates.Max() : 0,
        };
    }

    private static CategoryLearningStat BuildCategoryStat(RenovationCategory category, List<RenovationProject> projects)
    {
        var costsPerSqm = projects.Select(p => p.CostPerSqm).ToList();

        return new CategoryLearningStat
        {
            Category = category,
            SampleCount = projects.Count,
            TotalArea = projects.Sum(p => p.AreaSqm),
            TotalCost = projects.Sum(p => p.ActualTotalCost),
            MinCostPerSqm = costsPerSqm.Count > 0 ? costsPerSqm.Min() : 0,
            MaxCostPerSqm = costsPerSqm.Count > 0 ? costsPerSqm.Max() : 0,
        };
    }

    private static string MostFrequentUnit(IEnumerable<RenovationLineItem> items) =>
        items.GroupBy(li => li.Unit)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "m²";

    public static readonly Dictionary<RenovationTrade, string> TradeDisplayNames = new()
    {
        [RenovationTrade.BadSanitaer] = "Bad/Sanitär",
        [RenovationTrade.Bodenbelag] = "Bodenbelag",
        [RenovationTrade.MalerUndWaende] = "Maler/Wände",
        [RenovationTrade.Elektrik] = "Elektrik",
        [RenovationTrade.Heizung] = "Heizung",
        [RenovationTrade.FensterUndTueren] = "Fenster/Türen",
        [RenovationTrade.Dach] = "Dach",
        [RenovationTrade.Trockenbau] = "Trockenbau",
        [RenovationTrade.Fassade] = "Fassade",
        [RenovationTrade.Kueche] = "Küche",
        [RenovationTrade.Sonstiges] = "Sonstiges",
    };

    public static readonly Dictionary<RenovationStatus, string> StatusDisplayNames = new()
    {
        [RenovationStatus.Geplant] = "Geplant",
        [RenovationStatus.InUmsetzung] = "In Umsetzung",
        [RenovationStatus.Abgeschlossen] = "Abgeschlossen",
    };

    public static readonly Dictionary<RenovationCategory, string> CategoryDisplayNames = new()
    {
        [RenovationCategory.Badsanierung] = "Badsanierung",
        [RenovationCategory.Kuechensanierung] = "Küchensanierung",
        [RenovationCategory.Vollsanierung] = "Vollsanierung",
        [RenovationCategory.Bodensanierung] = "Bodensanierung",
        [RenovationCategory.Fassadensanierung] = "Fassadensanierung",
        [RenovationCategory.Dachsanierung] = "Dachsanierung",
        [RenovationCategory.Fenstersanierung] = "Fenstersanierung",
        [RenovationCategory.Heizungssanierung] = "Heizungssanierung",
        [RenovationCategory.Sonstiges] = "Sonstiges",
    };
}
