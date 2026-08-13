namespace Immomanager.Web.Models;

/// <summary>Eine Zeile im Soll-Ist-Vergleich der Miete pro m² für ein Quartal.</summary>
public class RentTargetComparisonRow
{
    public int RentTargetId { get; set; }
    public int Year { get; set; }
    public int Quarter { get; set; }
    public decimal TargetRentPerSqm { get; set; }
    public decimal ActualRentPerSqm { get; set; }

    public decimal DeviationPerSqm => ActualRentPerSqm - TargetRentPerSqm;
    public decimal DeviationPercent => TargetRentPerSqm > 0 ? DeviationPerSqm / TargetRentPerSqm * 100 : 0;

    /// <summary>Ob dies das aktuelle (heutige) Kalenderquartal ist.</summary>
    public bool IsCurrentQuarter { get; set; }
}
