namespace Immomanager.Web.Models;

/// <summary>Von Claude aus einer Policen-PDF extrahierte Vertragsfakten und Checklisten-Ergebnisse.</summary>
public class InsurancePolicyAnalysisResult
{
    public string? Provider { get; set; }
    public string? PolicyNumber { get; set; }
    public decimal? AnnualPremium { get; set; }

    /// <summary>Als Text extrahiert (Freitext-Datumsformat aus der Police), wird beim Übernehmen
    /// nachsichtig geparst - bei Fehlschlag bleibt das jeweilige Datum unverändert.</summary>
    public string? StartDate { get; set; }
    public string? ExpirationDate { get; set; }

    public string? Summary { get; set; }

    public List<InsurancePolicyCheckFinding> CheckFindings { get; set; } = new();
}

/// <summary>Ein einzelnes Prüfergebnis, referenziert über den stabilen Katalog-Key
/// (<see cref="Services.InsuranceCheckCatalog"/>).</summary>
public class InsurancePolicyCheckFinding
{
    public string Key { get; set; } = string.Empty;

    public bool? Covered { get; set; }

    public string? Note { get; set; }
}
