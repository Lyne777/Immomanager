using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

/// <summary>Fester Katalog der Versicherungs-Prüfpunkte, aus dem für jede Immobilie die
/// <see cref="InsuranceCheckItem"/>-Zeilen erzeugt werden. Der stabile "Key" macht das Seeding
/// idempotent (auch nachträgliches Ergänzen neuer Katalog-Einträge in bestehenden Objekten) und
/// erlaubt Armin Asset, Analyseergebnisse eindeutig einer Position zuzuordnen.</summary>
public static class InsuranceCheckCatalog
{
    public sealed record TemplateItem(string Key, InsuranceCategory Category, string? GroupLabel, string Title, int SortOrder);

    public static readonly IReadOnlyList<TemplateItem> Items = new List<TemplateItem>
    {
        new("geb.grundgefahren", InsuranceCategory.Gebaeudeversicherung, null,
            "Grundgefahren: Feuer, Leitungswasser, Sturm & Hagel eingeschlossen?", 1),
        new("geb.elementarschutz", InsuranceCategory.Gebaeudeversicherung, null,
            "Elementarschutz: Starkregen, Überschwemmung, Rückstau (Kanalisation), Erdbeben/Erdrutsch abgedeckt?", 2),
        new("geb.weg.police_vorhanden", InsuranceCategory.Gebaeudeversicherung, "WEG-Prüfung (bei Eigentumswohnungen)",
            "WEG-Police & Beitragsrechnung der Hausverwaltung liegt vor?", 3),
        new("geb.weg.elementarschutz", InsuranceCategory.Gebaeudeversicherung, "WEG-Prüfung (bei Eigentumswohnungen)",
            "Elementarschutz in der WEG-Police enthalten?", 4),
        new("geb.klauseln.grobe_fahrlaessigkeit", InsuranceCategory.Gebaeudeversicherung, "Klauseln & Kleingedrucktes",
            "Grobe Fahrlässigkeit zu 100 % mitversichert?", 5),
        new("geb.klauseln.leerstand", InsuranceCategory.Gebaeudeversicherung, "Klauseln & Kleingedrucktes",
            "Leerstehende/unbewohnte Einheiten mitversichert?", 6),
        new("geb.klauseln.ableitungsrohre", InsuranceCategory.Gebaeudeversicherung, "Klauseln & Kleingedrucktes",
            "Ableitungsrohre außerhalb des Gebäudes abgedeckt?", 7),
        new("geb.anpassung", InsuranceCategory.Gebaeudeversicherung, null,
            "Anpassung: An aktuellen Baupreisindex / gleitenden Neuwertfaktor angepasst (keine Unterversicherung)?", 8),

        new("haftpflicht.deckungssumme", InsuranceCategory.HausUndGrundbesitzerhaftpflicht, null,
            "Deckungssumme: Mindestens 10 bis 15 Mio. € pauschal für Personen- und Sachschäden?", 1),
        new("haftpflicht.verkehrssicherung", InsuranceCategory.HausUndGrundbesitzerhaftpflicht, null,
            "Verkehrssicherungspflichten: Streupflicht/Winterdienst, herabfallende Dachziegel, Mängel im Treppenhaus abgesichert?", 2),
        new("haftpflicht.bauarbeiten", InsuranceCategory.HausUndGrundbesitzerhaftpflicht, null,
            "Bauarbeiten: Kleinere Umbau-/Sanierungsmaßnahmen (Bauherrenrisiko) bis z. B. 100.000 € mitversichert?", 3),
    };
}
