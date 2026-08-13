namespace Immomanager.Web.Models;

/// <summary>Prüfung der 15%-Grenze für anschaffungsnahe Herstellungskosten (§ 6 Abs. 1 Nr. 1a EStG):
/// übersteigt die anfängliche Sanierung 15 % des Gebäudeanteils, ist sie nicht sofort abzugsfähig,
/// sondern muss über die AfA verteilt (aktiviert) werden.</summary>
public class RenovationTaxCheck
{
    public decimal BuildingValue { get; set; }
    public decimal ThresholdAmount { get; set; }
    public decimal RenovationCost { get; set; }

    public bool IsExceeded => RenovationCost > ThresholdAmount;
}
