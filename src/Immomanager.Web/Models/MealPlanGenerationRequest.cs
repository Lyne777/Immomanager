namespace Immomanager.Web.Models;

/// <summary>Eingabeparameter für die KI-Generierung eines <see cref="MealPlan"/> - siehe
/// <see cref="Services.IMealPlanGenerationService"/>.</summary>
public class MealPlanGenerationRequest
{
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public int DayCount { get; set; } = 7;

    public bool IncludeFruehstueck { get; set; } = true;

    public bool IncludeMittag { get; set; } = true;

    public bool IncludeAbend { get; set; } = true;

    /// <summary>Fokus auf entzündungshemmende Ernährung (viel Gemüse/Obst, Omega-3, wenig Zucker/
    /// stark verarbeitete Lebensmittel).</summary>
    public bool AntiInflammatory { get; set; } = true;

    /// <summary>Moderates Kaloriendefizit zur Gewichtsreduktion.</summary>
    public bool CalorieDeficit { get; set; } = true;

    /// <summary>Freie Wünsche/Ausschlüsse, z. B. "keine Pilze", "1x pro Woche Fisch reicht".</summary>
    public string? Notes { get; set; }
}
