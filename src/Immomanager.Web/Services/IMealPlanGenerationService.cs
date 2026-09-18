using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public interface IMealPlanGenerationService
{
    bool IsConfigured { get; }

    /// <summary>Lässt Claude einen Wochenspeiseplan erzeugen. Liefert ein noch NICHT gespeichertes
    /// <see cref="MealPlan"/> zur Vorschau - das Speichern erfolgt erst über einen expliziten
    /// Folgeaufruf von <see cref="IMealPlanService.CreateAsync"/>.</summary>
    Task<MealPlan> GenerateAsync(MealPlanGenerationRequest request, CancellationToken cancellationToken = default);
}
