using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

/// <summary>Vergleicht die geplante (Soll-)Miete pro m² je Quartal mit der aktuellen Ist-Miete pro m²
/// einer Immobilie und erkennt fehlende Soll-Werte für das laufende Quartal. Da die App keine
/// historische Ist-Miete je Quartal führt (nur den aktuellen Stand auf der Immobilie), wird die
/// aktuelle Ist-Miete pro m² als Vergleichswert für alle Quartale herangezogen.</summary>
public class RentTargetAnalyticsService
{
    public static (int Year, int Quarter) GetQuarterOf(DateTime date) => (date.Year, (date.Month - 1) / 3 + 1);

    /// <summary>Einheiten, deren Fläche/Miete in die €/m²-Berechnung einfließen - z. B. Garagen/
    /// Stellplätze werden über <see cref="PropertyUnit.CountsTowardRentTarget"/> ausgeschlossen, da
    /// für sie kein Wohnflächen-Mietspiegel gilt und sie den €/m²-Wert sonst verzerren würden.</summary>
    public static List<PropertyUnit> GetRentTargetRelevantUnits(Property property) =>
        property.Units.Where(u => u.CountsTowardRentTarget).ToList();

    public List<RentTargetComparisonRow> BuildComparison(Property property)
    {
        var relevantUnits = GetRentTargetRelevantUnits(property);
        var relevantAreaSqm = relevantUnits.Sum(u => u.AreaSqm);
        var relevantColdRentMonthly = relevantUnits.Sum(u => u.ColdRentMonthly);
        var actualRentPerSqm = relevantAreaSqm > 0 ? relevantColdRentMonthly / relevantAreaSqm : 0;
        var (currentYear, currentQuarter) = GetQuarterOf(DateTime.Today);

        return property.RentTargets
            .OrderBy(t => t.Year).ThenBy(t => t.Quarter)
            .Select(t => new RentTargetComparisonRow
            {
                RentTargetId = t.Id,
                Year = t.Year,
                Quarter = t.Quarter,
                TargetRentPerSqm = t.TargetRentPerSqm,
                ActualRentPerSqm = actualRentPerSqm,
                IsCurrentQuarter = t.Year == currentYear && t.Quarter == currentQuarter,
            })
            .ToList();
    }

    /// <summary>Ob für das laufende Kalenderquartal noch kein Soll-Wert hinterlegt ist.</summary>
    public bool IsMissingCurrentQuarterTarget(Property property)
    {
        var (year, quarter) = GetQuarterOf(DateTime.Today);
        return !property.RentTargets.Any(t => t.Year == year && t.Quarter == quarter);
    }
}
