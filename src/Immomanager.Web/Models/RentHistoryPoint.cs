namespace Immomanager.Web.Models;

/// <summary>Ein Stützpunkt der Kaltmieten-Entwicklung eines Objekts (Gesamtobjekt oder anteilig je
/// ViewMode) - siehe <see cref="Services.KpiCalculationService.BuildRentHistory"/>.</summary>
public class RentHistoryPoint
{
    public DateOnly Date { get; set; }

    public decimal ColdRentMonthly { get; set; }
}
