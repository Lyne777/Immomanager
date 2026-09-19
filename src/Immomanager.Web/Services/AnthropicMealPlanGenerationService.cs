using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Immomanager.Web.Models;
using Microsoft.Extensions.Options;

namespace Immomanager.Web.Services;

/// <summary>Lässt Claude über die Anthropic-API (Structured Outputs, wie bei der Exposé-Analyse)
/// einen Wochenspeiseplan samt Zutatenlisten generieren.</summary>
public class AnthropicMealPlanGenerationService : IMealPlanGenerationService
{
    private static readonly string[] MealTypeValues = ["Fruehstueck", "Mittag", "Abend"];

    private static readonly string[] CategoryValues =
    [
        "Obst & Gemüse", "Fleisch & Fisch", "Milchprodukte & Eier", "Getreide & Backwaren",
        "Vorräte & Gewürze", "Sonstiges",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // Bewusst "minItems"/"maxItems" für "days" und "meals" gesetzt (statt sich nur auf die Anzahl im
    // Prompt-Text zu verlassen): ohne diese Vorgabe im Schema selbst hat sich gezeigt, dass die KI die
    // gewünschte Tagesanzahl nicht zuverlässig einhält (im schlimmsten Fall nur 1 Tag statt der
    // angeforderten Woche) - das Schema erzwingt die exakte Anzahl strukturell, statt sich auf die
    // Textanweisung allein zu verlassen.
    private static Dictionary<string, JsonElement> BuildResponseSchema(MealPlanGenerationRequest request)
    {
        var mealCount = new[] { request.IncludeFruehstueck, request.IncludeMittag, request.IncludeAbend }.Count(x => x);

        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>($$"""
            {
              "type": "object",
              "properties": {
                "days": {
                  "type": "array",
                  "minItems": {{request.DayCount}},
                  "maxItems": {{request.DayCount}},
                  "items": {
                    "type": "object",
                    "properties": {
                      "dayOffset": { "type": "integer" },
                      "meals": {
                        "type": "array",
                        "minItems": {{mealCount}},
                        "maxItems": {{mealCount}},
                        "items": {
                          "type": "object",
                          "properties": {
                            "mealType": { "type": "string", "enum": {{JsonSerializer.Serialize(MealTypeValues)}} },
                            "title": { "type": "string" },
                            "description": { "type": "string" },
                            "ingredients": {
                              "type": "array",
                              "items": {
                                "type": "object",
                                "properties": {
                                  "name": { "type": "string" },
                                  "quantity": { "type": ["number", "null"] },
                                  "unit": { "type": ["string", "null"] },
                                  "category": { "type": "string", "enum": {{JsonSerializer.Serialize(CategoryValues)}} }
                                },
                                "required": ["name", "quantity", "unit", "category"],
                                "additionalProperties": false
                              }
                            }
                          },
                          "required": ["mealType", "title", "description", "ingredients"],
                          "additionalProperties": false
                        }
                      }
                    },
                    "required": ["dayOffset", "meals"],
                    "additionalProperties": false
                  }
                }
              },
              "required": ["days"],
              "additionalProperties": false
            }
            """)!;
    }

    private readonly IOptionsMonitor<AnthropicOptions> _optionsMonitor;
    private readonly ILogger<AnthropicMealPlanGenerationService> _logger;

    public AnthropicMealPlanGenerationService(IOptionsMonitor<AnthropicOptions> optionsMonitor, ILogger<AnthropicMealPlanGenerationService> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_optionsMonitor.CurrentValue.ApiKey);

    public async Task<MealPlan> GenerateAsync(MealPlanGenerationRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Kein Anthropic API-Key hinterlegt. Bitte unter \"Einstellungen\" in der Navigation konfigurieren.");
        }

        var options = _optionsMonitor.CurrentValue;
        AnthropicClient client = new() { ApiKey = options.ApiKey };

        var mealCount = new[] { request.IncludeFruehstueck, request.IncludeMittag, request.IncludeAbend }.Count(x => x);
        // Grob nach Umfang skaliert (statt fixem Wert) - eine volle Woche mit 3 Mahlzeiten/Tag braucht
        // deutlich mehr Tokens als ein 2-Tage-Plan, sonst droht die Antwort bei größeren Plänen
        // mitten in der Zutatenliste abgeschnitten zu werden.
        var maxTokens = Math.Clamp(1500 + request.DayCount * mealCount * 600, 2000, 16000);

        Message response;
        try
        {
            response = await client.Messages.Create(new MessageCreateParams
            {
                Model = options.Model,
                MaxTokens = maxTokens,
                System = BuildSystemPrompt(request),
                OutputConfig = new OutputConfig
                {
                    Format = new JsonOutputFormat { Schema = BuildResponseSchema(request) },
                },
                Messages = [new() { Role = Role.User, Content = BuildUserPrompt(request) }],
            }, cancellationToken);
        }
        catch (AnthropicUnauthorizedException ex)
        {
            _logger.LogError(ex, "Anthropic API-Key ungültig bei Speiseplan-Generierung.");
            throw new InvalidOperationException("Der Anthropic API-Key ist ungültig oder abgelaufen.");
        }
        catch (AnthropicRateLimitException ex)
        {
            _logger.LogWarning(ex, "Anthropic-Anfragelimit bei Speiseplan-Generierung erreicht.");
            throw new InvalidOperationException("Anthropic-Anfragelimit erreicht - bitte kurz warten und erneut versuchen.");
        }
        catch (AnthropicApiException ex)
        {
            _logger.LogError(ex, "Anthropic-API-Fehler bei Speiseplan-Generierung.");
            throw new InvalidOperationException($"Fehler bei der KI-Generierung: {ex.Message}");
        }

        if (response.StopReason == "refusal")
        {
            throw new InvalidOperationException("Die KI hat die Generierung dieses Speiseplans abgelehnt.");
        }

        var jsonText = response.Content.Select(b => b.Value).OfType<TextBlock>().FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            throw new InvalidOperationException("Die KI hat keine auswertbare Antwort geliefert.");
        }

        var result = JsonSerializer.Deserialize<MealPlanGenerationResult>(jsonText, JsonOptions)
            ?? throw new InvalidOperationException("Die KI-Antwort konnte nicht gelesen werden.");

        return ToMealPlan(request, result);
    }

    private static string BuildSystemPrompt(MealPlanGenerationRequest request)
    {
        var focusPoints = new List<string>();
        if (request.AntiInflammatory)
        {
            focusPoints.Add(
                "entzündungshemmend: viel Gemüse und Obst, Omega-3-reicher Fisch (z. B. Lachs, Makrele) " +
                "mehrmals pro Woche, hochwertige pflanzliche Öle (Oliven-/Leinöl), Vollkornprodukte, " +
                "wenig Zucker, wenig stark verarbeitete Lebensmittel und wenig rotes/verarbeitetes Fleisch");
        }
        if (request.CalorieDeficit)
        {
            focusPoints.Add("moderates Kaloriendefizit zur schrittweisen, gesunden Gewichtsreduktion (kein radikales Diäten)");
        }

        var focusText = focusPoints.Count > 0
            ? "Ernährungsschwerpunkte: " + string.Join("; ", focusPoints) + "."
            : "Keine besonderen Ernährungsschwerpunkte.";

        return $"""
            Du erstellst alltagstaugliche Wochenspeisepläne für einen 2-Personen-Haushalt in Deutschland.
            {focusText}
            Die Gerichte sollen mit normalem Aufwand nachkochbar sein und ihre Zutaten in einem
            durchschnittlichen deutschen Supermarkt (z. B. Rewe/Edeka) erhältlich sein. "description" ist
            eine kurze, stichpunktartige Zubereitungsanleitung auf Deutsch (3-6 Schritte). Mengenangaben
            in "ingredients" beziehen sich auf die tatsächliche Portionenzahl - gib realistische Mengen mit
            gängigen Einheiten an (g, kg, ml, l, Stück, Bund, Dose, Packung). Ist eine Zutat nicht sinnvoll
            zu bemessen (z. B. "Salz nach Geschmack"), setze "quantity" und "unit" auf null. Wähle "category"
            IMMER aus der vorgegebenen Liste passend zur Zutat. Wiederhole Zutaten über mehrere Mahlzeiten
            hinweg bewusst (z. B. dieselbe Gemüsesorte), um Einkauf und Reste sinnvoll zu halten, statt für
            jede Mahlzeit komplett neue Zutaten zu erfinden.
            """;
    }

    private static string BuildUserPrompt(MealPlanGenerationRequest request)
    {
        var mealTypes = new List<string>();
        if (request.IncludeFruehstueck) mealTypes.Add("Frühstück");
        if (request.IncludeMittag) mealTypes.Add("Mittagessen");
        if (request.IncludeAbend) mealTypes.Add("Abendessen");

        var prompt = $"Erstelle einen Speiseplan für GENAU {request.DayCount} Tage (nicht weniger!) ab dem " +
            $"{request.StartDate:yyyy-MM-dd} (Tag 1 = dayOffset 0, letzter Tag = dayOffset {request.DayCount - 1}) " +
            $"mit jeweils folgenden Mahlzeiten pro Tag: {string.Join(", ", mealTypes)}. Das \"days\"-Array muss " +
            $"also genau {request.DayCount} Einträge enthalten.";

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            prompt += $" Zusätzliche Wünsche/Ausschlüsse des Nutzers: {request.Notes.Trim()}";
        }

        return prompt;
    }

    private static MealPlan ToMealPlan(MealPlanGenerationRequest request, MealPlanGenerationResult result)
    {
        var plan = new MealPlan { StartDate = request.StartDate };

        foreach (var day in result.Days.OrderBy(d => d.DayOffset))
        {
            var date = request.StartDate.AddDays(day.DayOffset);
            foreach (var meal in day.Meals)
            {
                if (!Enum.TryParse<MealType>(meal.MealType, out var mealType))
                {
                    continue;
                }

                plan.Meals.Add(new PlannedMeal
                {
                    Date = date,
                    MealType = mealType,
                    Title = meal.Title,
                    Description = meal.Description,
                    Ingredients = meal.Ingredients.Select(i => new MealIngredient
                    {
                        Name = i.Name,
                        Quantity = i.Quantity,
                        Unit = i.Unit,
                        Category = i.Category,
                    }).ToList(),
                });
            }
        }

        return plan;
    }
}
