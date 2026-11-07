using System.ComponentModel.DataAnnotations;

namespace Immomanager.Web.Models;

/// <summary>Eine Komponente der Abwiege-Liste einer <see cref="PlannedMeal"/> für EINE Portion (z. B.
/// "Pellkartoffeln, gekocht", 200 g, 150 kcal) - Gewicht im zubereiteten Zustand, so wie es auf den
/// Teller kommt. Bei als Ganzes angerichteten Gerichten (Auflauf, Eintopf, Suppe) genügt ein einziger
/// Eintrag. Als JSON-Spalte an der Mahlzeit gespeichert (kein eigener Tabellen-Lebenszyklus).</summary>
public class MealPortionItem
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gewicht der Komponente in Gramm (bei Flüssigkeiten ml ≈ g).</summary>
    public int Grams { get; set; }

    /// <summary>Geschätzte Kilokalorien dieser Menge.</summary>
    public int Calories { get; set; }
}
