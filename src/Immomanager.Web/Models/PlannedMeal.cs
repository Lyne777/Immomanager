using System.ComponentModel.DataAnnotations;

namespace Immomanager.Web.Models;

/// <summary>Eine einzelne Mahlzeit (z. B. "Abendessen am 12.09.") innerhalb eines <see cref="MealPlan"/>.</summary>
public class PlannedMeal
{
    public int Id { get; set; }

    public int MealPlanId { get; set; }

    public MealPlan? MealPlan { get; set; }

    public DateOnly Date { get; set; }

    public MealType MealType { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Kurzes Rezept/Zubereitungshinweise.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Für wie viele Portionen die Mengen in <see cref="Ingredients"/> angegeben sind - Basis für
    /// die Umrechnung auf die Personenzahl beim Erstellen der Einkaufsliste.</summary>
    [Range(1, 20)]
    public int Servings { get; set; } = DefaultServings;

    public const int DefaultServings = 2;

    /// <summary>Von der KI geschätzte Kilokalorien pro Portion; 0 = unbekannt (Pläne aus der Zeit vor
    /// dieser Angabe).</summary>
    public int CaloriesPerServing { get; set; }

    /// <summary>Abwiege-Liste für eine Portion (Gramm je Komponente im zubereiteten Zustand mit
    /// Kilokalorien); leer bei Plänen aus der Zeit vor dieser Angabe.</summary>
    public List<MealPortionItem> PortionItems { get; set; } = new();

    public List<MealIngredient> Ingredients { get; set; } = new();
}
