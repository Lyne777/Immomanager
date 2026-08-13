namespace Immomanager.Web.Models;

/// <summary>Von Claude aus einem Mietvertrag extrahierte Eckdaten.</summary>
public class LeaseAnalysisResult
{
    public string? TenantName { get; set; }
    public string? TenantEmail { get; set; }
    public string? TenantPhone { get; set; }

    /// <summary>Als Text extrahiert, wird beim Übernehmen nachsichtig geparst - bei Fehlschlag
    /// bleibt das jeweilige Datum unverändert.</summary>
    public string? MoveInDate { get; set; }
    public string? MoveOutDate { get; set; }

    public decimal? ColdRentMonthly { get; set; }
    public decimal? AdvancePaymentMonthly { get; set; }
    public decimal? SecurityDeposit { get; set; }

    public string? Summary { get; set; }
}
