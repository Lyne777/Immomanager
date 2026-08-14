using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Immomanager.Web.Models;

/// <summary>Ein einzelner, frei formulierter Eintrag im Objekt-Logbuch (z. B. "Dach neu eingedeckt"),
/// unabhängig von den strukturierten <see cref="RenovationProject"/>-Datensätzen - für Dinge, die man
/// z. B. im Ankaufsprozess erfährt und einfach nur für später notieren will, ohne dafür ein volles
/// Renovierungsprojekt mit Budget/Gewerken anzulegen. Wird im Logbuch-Tab gemeinsam mit den
/// Renovierungsprojekten chronologisch angezeigt (siehe <see cref="PropertyLogService"/>).</summary>
public class PropertyLogEntry
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property? Property { get; set; }

    /// <summary>Optionaler Bezug zu einer einzelnen Einheit statt dem ganzen Objekt (z. B. "Bad DG").</summary>
    public int? PropertyUnitId { get; set; }

    [ForeignKey(nameof(PropertyUnitId))]
    public PropertyUnit? PropertyUnit { get; set; }

    /// <summary>Freitext für den Zeitpunkt (z. B. "2009", "1998/1999", "Frühjahr 2021") - bewusst kein
    /// striktes Datum, da der genaue Zeitpunkt vieler Altbestands-Ereignisse oft nicht bekannt ist.</summary>
    [Required, StringLength(50)]
    public string DateLabel { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Description { get; set; } = string.Empty;
}
