using System.Text.Json.Serialization;

namespace Immomanager.Web.Models;

/// <summary>Von der KI aus einem Exposé-PDF extrahierte Werte. Alle Felder sind nullable - fehlt eine
/// Information im Dokument, liefert die KI null statt eines geratenen Werts.</summary>
public class ExposeAnalysisResult
{
    [JsonPropertyName("objectName")]
    public string? ObjectName { get; set; }

    [JsonPropertyName("street")]
    public string? Street { get; set; }

    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("purchasePrice")]
    public decimal? PurchasePrice { get; set; }

    [JsonPropertyName("livingAreaSqm")]
    public decimal? LivingAreaSqm { get; set; }

    [JsonPropertyName("yearBuilt")]
    public int? YearBuilt { get; set; }

    [JsonPropertyName("unitCount")]
    public int? UnitCount { get; set; }

    [JsonPropertyName("parkingSpaces")]
    public int? ParkingSpaces { get; set; }

    [JsonPropertyName("netColdRentMonthly")]
    public decimal? NetColdRentMonthly { get; set; }

    [JsonPropertyName("nonAllocableCostsMonthly")]
    public decimal? NonAllocableCostsMonthly { get; set; }

    [JsonPropertyName("maintenanceReserveInHousingFee")]
    public decimal? MaintenanceReserveInHousingFee { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    /// <summary>Ob mindestens ein Feld erkannt wurde (für die Anzeige "nichts gefunden").</summary>
    [JsonIgnore]
    public bool HasAnyValue =>
        ObjectName is not null || Street is not null || PostalCode is not null || City is not null ||
        PurchasePrice is not null || LivingAreaSqm is not null || YearBuilt is not null ||
        UnitCount is not null || ParkingSpaces is not null || NetColdRentMonthly is not null ||
        NonAllocableCostsMonthly is not null || MaintenanceReserveInHousingFee is not null;
}
