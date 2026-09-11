using System.ComponentModel.DataAnnotations;

namespace Immomanager.Web.Models;

/// <summary>Ein Eigentümer (Person oder Gesellschaft), über den eine oder mehrere Immobilien gehalten
/// werden. Dient Armin Asset als automatische Absender-Quelle beim Erstellen von Mieterschreiben,
/// damit Name/Adresse nicht bei jedem Schreiben erneut abgefragt werden müssen.</summary>
public class Owner
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Absenderadresse für Briefe (mehrzeilig, z. B. Straße + PLZ/Ort).</summary>
    [StringLength(500)]
    public string? Address { get; set; }

    /// <summary>Relativer Pfad zum hochgeladenen Briefkopf (.docx) unterhalb des Datenverzeichnisses.
    /// Wird von der Mieterschreiben-Generierung als Vorlage verwendet: der von Armin formulierte
    /// Brieftext wird an das bestehende Dokument angehängt, sodass dessen Kopf-/Fußzeile (Logo,
    /// Kontaktdaten) automatisch übernommen wird.</summary>
    public string? LetterheadFilePath { get; set; }

    public string? LetterheadFileName { get; set; }

    public List<Property> Properties { get; set; } = new();
}
