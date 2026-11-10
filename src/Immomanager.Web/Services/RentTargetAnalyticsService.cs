using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

/// <summary>Vergleicht die geplante (Soll-)Miete pro m² je Quartal mit der aktuellen Ist-Miete pro m²
/// einer Immobilie und erkennt fehlende Soll-Werte für das zuletzt abgeschlossene Quartal (der
/// Mietspiegel-Wert ist erst nach Quartalsende verfügbar, das laufende Quartal wird daher nicht
/// bemängelt). Da die App keine historische Ist-Miete je Quartal führt (nur den aktuellen Stand auf
/// der Immobilie), wird die aktuelle Ist-Miete pro m² als Vergleichswert für alle Quartale
/// herangezogen.</summary>
public class RentTargetAnalyticsService
{
    public static (int Year, int Quarter) GetQuarterOf(DateTime date) => (date.Year, (date.Month - 1) / 3 + 1);

    /// <summary>Das letzte vollständig vergangene Kalenderquartal vor <paramref name="date"/>.</summary>
    public static (int Year, int Quarter) GetLastCompletedQuarter(DateTime date)
    {
        var (year, quarter) = GetQuarterOf(date);
        return quarter == 1 ? (year - 1, 4) : (year, quarter - 1);
    }

    /// <summary>Einheiten, deren Fläche/Miete in die €/m²-Berechnung einfließen - z. B. Garagen/
    /// Stellplätze werden über <see cref="PropertyUnit.CountsTowardRentTarget"/> ausgeschlossen, da
    /// für sie kein Wohnflächen-Mietspiegel gilt und sie den €/m²-Wert sonst verzerren würden.</summary>
    public static List<PropertyUnit> GetRentTargetRelevantUnits(Property property) =>
        property.Units.Where(u => u.CountsTowardRentTarget).ToList();

    /// <summary>Die Einheiten, über die der Ist-€/m²-Wert tatsächlich gerechnet wird: für die Soll-Miete
    /// relevante Einheiten (<see cref="GetRentTargetRelevantUnits"/>), die aktuell vermietet sind.
    /// Leerstand hat keine Miete und würde den Wert sonst künstlich nach unten ziehen, obwohl er das
    /// Mietniveau gegenüber dem Mietspiegel zeigen soll. Einzige Quelle für Rechnung und Anzeige der
    /// Berechnungsbasis, damit beide nicht auseinanderlaufen.</summary>
    public static List<PropertyUnit> GetRentedRelevantUnits(Property property) =>
        GetRentTargetRelevantUnits(property).Where(u => u.CurrentTenancy is not null).ToList();

    public List<RentTargetComparisonRow> BuildComparison(Property property)
    {
        var rentedUnits = GetRentedRelevantUnits(property);
        var rentedAreaSqm = rentedUnits.Sum(u => u.AreaSqm);
        var rentedColdRentMonthly = rentedUnits.Sum(u => u.ColdRentMonthly);
        var actualRentPerSqm = rentedAreaSqm > 0 ? rentedColdRentMonthly / rentedAreaSqm : 0;
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

    /// <summary>Ob für das zuletzt abgeschlossene Kalenderquartal noch kein Soll-Wert hinterlegt ist.
    /// Objekte, die erst nach Ende dieses Quartals gekauft wurden, gehörten in dem Quartal noch nicht
    /// zum Bestand und werden nicht bemängelt.</summary>
    public bool IsMissingLastCompletedQuarterTarget(Property property)
    {
        var (year, quarter) = GetLastCompletedQuarter(DateTime.Today);
        var quarterEnd = new DateOnly(year, quarter * 3, 1).AddMonths(1).AddDays(-1);
        return property.PurchaseDate <= quarterEnd
            && !property.RentTargets.Any(t => t.Year == year && t.Quarter == quarter);
    }
}
