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
    private static readonly string[] CategoryValues =
    [
        "Obst & Gemüse", "Fleisch & Fisch", "Milchprodukte & Eier", "Getreide & Backwaren",
        "Vorräte & Gewürze", "Sonstiges",
    ];

    // Anthropics Structured Outputs unterstützen bei Arrays nur "minItems" 0 oder 1 (ein fixes
    // minItems=7 z. B. für eine Wochen-Ansicht wird als ungültiges Schema abgelehnt) - deshalb bewusst
    // KEIN Array für Tage/Mahlzeiten, sondern feste, benannte Pflichtfelder ("day0".."dayN-1" bzw.
    // "fruehstueck"/"mittag"/"abend"). "required" auf Objektebene erzwingt die gewünschte Anzahl
    // stattdessen strukturell zuverlässig - Arrays bleiben nur für die (unbegrenzte) Zutatenliste.
    private static Dictionary<string, JsonElement> BuildResponseSchema(MealPlanGenerationRequest request)
    {
        var mealKeys = MealKeys(request);

        object mealSchema = new
        {
            type = "object",
            properties = new
            {
                title = new { type = "string" },
                description = new { type = "string" },
                ingredients = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string" },
                            // Bewusst NICHT nullable (kein "type": ["number","null"]): Anthropics
                            // Structured Outputs begrenzen die Anzahl "union-typed" Felder im gesamten
                            // Schema (Fehler "too many parameters with union types" ab genau diesem
                            // Muster) - bei 7 Tagen x 3 Mahlzeiten würde sich ein nullable Feld 21x
                            // duplizieren und das Limit sprengen. Ist eine Zutat nicht sinnvoll zu
                            // bemessen, kommt stattdessen 0/"" als Konvention (siehe Prompt), das
                            // Parsing unten übersetzt das zurück in null.
                            quantity = new { type = "number" },
                            unit = new { type = "string" },
                            category = new { type = "string", @enum = CategoryValues },
                        },
                        required = new[] { "name", "quantity", "unit", "category" },
                        additionalProperties = false,
                    },
                },
            },
            required = new[] { "title", "description", "ingredients" },
            additionalProperties = false,
        };

        var dayProperties = mealKeys.ToDictionary(key => key, _ => mealSchema);
        object daySchema = new
        {
            type = "object",
            properties = dayProperties,
            required = mealKeys,
            additionalProperties = false,
        };

        var dayKeys = Enumerable.Range(0, request.DayCount).Select(i => $"day{i}").ToList();
        var topProperties = dayKeys.ToDictionary(key => key, _ => daySchema);

        var fullSchema = new
        {
            type = "object",
            properties = topProperties,
            required = dayKeys,
            additionalProperties = false,
        };

        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(fullSchema))!;
    }

    private static List<string> MealKeys(MealPlanGenerationRequest request)
    {
        var keys = new List<string>();
        if (request.IncludeFruehstueck) keys.Add("fruehstueck");
        if (request.IncludeMittag) keys.Add("mittag");
        if (request.IncludeAbend) keys.Add("abend");
        return keys;
    }

    private static bool TryParseMealType(string key, out MealType mealType)
    {
        switch (key)
        {
            case "fruehstueck": mealType = MealType.Fruehstueck; return true;
            case "mittag": mealType = MealType.Mittag; return true;
            case "abend": mealType = MealType.Abend; return true;
            default: mealType = default; return false;
        }
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

        var mealCount = MealKeys(request).Count;
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

        return ParseMealPlan(request, jsonText);
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
            zu bemessen (z. B. "Salz nach Geschmack"), setze "quantity" auf 0 und "unit" auf einen leeren
            String "". Wähle "category"
            IMMER aus der vorgegebenen Liste passend zur Zutat. Wiederhole Zutaten über mehrere Mahlzeiten
            hinweg bewusst (z. B. dieselbe Gemüsesorte), um Einkauf und Reste sinnvoll zu halten, statt für
            jede Mahlzeit komplett neue Zutaten zu erfinden. Das Antwortschema gibt für jeden Tag
            (Schlüssel "day0", "day1", ...) und jede angeforderte Mahlzeit (Schlüssel "fruehstueck"/
            "mittag"/"abend") ein Pflichtfeld vor - fülle wirklich JEDES davon aus, lass keins aus.
            """;
    }

    private static string BuildUserPrompt(MealPlanGenerationRequest request)
    {
        var mealTypes = new List<string>();
        if (request.IncludeFruehstueck) mealTypes.Add("Frühstück");
        if (request.IncludeMittag) mealTypes.Add("Mittagessen");
        if (request.IncludeAbend) mealTypes.Add("Abendessen");

        var prompt = $"Erstelle einen Speiseplan für {request.DayCount} Tage ab dem " +
            $"{request.StartDate:yyyy-MM-dd} (day0 = {request.StartDate:yyyy-MM-dd}, day1 = " +
            $"{request.StartDate.AddDays(1):yyyy-MM-dd}, usw.) mit jeweils folgenden Mahlzeiten pro Tag: " +
            $"{string.Join(", ", mealTypes)}.";

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            prompt += $" Zusätzliche Wünsche/Ausschlüsse des Nutzers: {request.Notes.Trim()}";
        }

        return prompt;
    }

    private static MealPlan ParseMealPlan(MealPlanGenerationRequest request, string jsonText)
    {
        var plan = new MealPlan { StartDate = request.StartDate };

        using var document = JsonDocument.Parse(jsonText);
        foreach (var dayProperty in document.RootElement.EnumerateObject())
        {
            if (!dayProperty.Name.StartsWith("day", StringComparison.Ordinal) ||
                !int.TryParse(dayProperty.Name.AsSpan(3), out var dayOffset))
            {
                continue;
            }

            var date = request.StartDate.AddDays(dayOffset);
            foreach (var mealProperty in dayProperty.Value.EnumerateObject())
            {
                if (!TryParseMealType(mealProperty.Name, out var mealType))
                {
                    continue;
                }

                var mealElement = mealProperty.Value;
                var ingredients = new List<MealIngredient>();
                if (mealElement.TryGetProperty("ingredients", out var ingredientsElement))
                {
                    foreach (var ingredientElement in ingredientsElement.EnumerateArray())
                    {
                        // "quantity"/"unit" sind im Schema nicht nullable (siehe BuildResponseSchema) -
                        // 0 bzw. "" ist die vereinbarte Konvention der KI für "nicht sinnvoll bezifferbar",
                        // hier zurück in echtes null übersetzt (das erwarten Aggregation/Anzeige).
                        var quantity = ingredientElement.GetProperty("quantity").GetDecimal();
                        var unit = ingredientElement.GetProperty("unit").GetString();

                        ingredients.Add(new MealIngredient
                        {
                            Name = ingredientElement.GetProperty("name").GetString() ?? string.Empty,
                            Quantity = quantity > 0 ? quantity : null,
                            Unit = string.IsNullOrWhiteSpace(unit) ? null : unit,
                            Category = ingredientElement.GetProperty("category").GetString() ?? "Sonstiges",
                        });
                    }
                }

                plan.Meals.Add(new PlannedMeal
                {
                    Date = date,
                    MealType = mealType,
                    Title = mealElement.TryGetProperty("title", out var titleElement) ? titleElement.GetString() ?? string.Empty : string.Empty,
                    Description = mealElement.TryGetProperty("description", out var descriptionElement) ? descriptionElement.GetString() ?? string.Empty : string.Empty,
                    Ingredients = ingredients,
                });
            }
        }

        return plan;
    }
}
