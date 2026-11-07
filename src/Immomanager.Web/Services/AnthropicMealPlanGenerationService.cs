using System.Diagnostics;
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
    //
    // Die Mahlzeiten-/Zutaten-Schemas werden bewusst NUR EINMAL unter "$defs" definiert und per
    // "$ref" referenziert statt an jedem der bis zu 21 Tag×Mahlzeit-Slots dupliziert zu werden -
    // ohne diese Wiederverwendung wird die von Anthropic intern kompilierte Grammatik bei größeren
    // Plänen zu groß ("The compiled grammar is too large").
    private static Dictionary<string, JsonElement> BuildResponseSchema(MealPlanGenerationRequest request)
    {
        var mealKeys = MealKeys(request);

        object ingredientSchema = new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string" },
                // Bewusst NICHT nullable (kein "type": ["number","null"]): Anthropics Structured
                // Outputs begrenzen die Anzahl "union-typed" Felder im gesamten Schema. Ist eine
                // Zutat nicht sinnvoll zu bemessen, kommt stattdessen 0/"" als Konvention (siehe
                // Prompt), das Parsing unten übersetzt das zurück in null.
                quantity = new { type = "number" },
                unit = new { type = "string" },
                category = new { type = "string", @enum = CategoryValues },
            },
            required = new[] { "name", "quantity", "unit", "category" },
            additionalProperties = false,
        };

        // Abwiege-Liste einer Portion: Gewicht und Kalorien je Komponente im zubereiteten Zustand.
        // Ganzzahlen (kein Union-Typ, zählt also nicht gegen das Anthropic-Limit).
        object portionItemSchema = new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string" },
                grams = new { type = "integer" },
                calories = new { type = "integer" },
            },
            required = new[] { "name", "grams", "calories" },
            additionalProperties = false,
        };

        object mealSchema = new
        {
            type = "object",
            properties = new
            {
                title = new { type = "string" },
                description = new { type = "string" },
                caloriesPerServing = new { type = "integer" },
                portionItems = new
                {
                    type = "array",
                    minItems = 1,
                    items = Ref("portionItem"),
                },
                ingredients = new
                {
                    type = "array",
                    items = Ref("ingredient"),
                },
            },
            required = new[] { "title", "description", "caloriesPerServing", "portionItems", "ingredients" },
            additionalProperties = false,
        };

        var dayProperties = mealKeys.ToDictionary(key => key, _ => Ref("meal"));
        object daySchema = new
        {
            type = "object",
            properties = dayProperties,
            required = mealKeys,
            additionalProperties = false,
        };

        var dayKeys = Enumerable.Range(0, request.DayCount).Select(i => $"day{i}").ToList();
        var topProperties = dayKeys.ToDictionary(key => key, _ => Ref("day"));

        var fullSchema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["$defs"] = new Dictionary<string, object>
            {
                ["ingredient"] = ingredientSchema,
                ["portionItem"] = portionItemSchema,
                ["meal"] = mealSchema,
                ["day"] = daySchema,
            },
            ["properties"] = topProperties,
            ["required"] = dayKeys,
            ["additionalProperties"] = false,
        };

        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(fullSchema))!;
    }

    private static Dictionary<string, object> Ref(string defName) => new() { ["$ref"] = $"#/$defs/{defName}" };

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

    // Obergrenze für eine einzelne Generierung (siehe Kommentar am Client weiter unten).
    private static readonly TimeSpan GenerationTimeout = TimeSpan.FromMinutes(10);

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
        // Ohne explizites Timeout leitet das SDK sein Zeitlimit aus MaxTokens ab (gemessen: ca. 0,03 s pro
        // erlaubtem Token, mindestens 30 s - bei 3 Mahlzeiten also nur ca. 2 Minuten). Das ist für
        // strukturierte Ausgaben (Grammatik-Kompilierung, langsamere Modelle) zu knapp und führte zu
        // Abbrüchen mitten in einer völlig normalen Generierung.
        AnthropicClient client = new() { ApiKey = options.ApiKey, Timeout = GenerationTimeout };

        var mealCount = MealKeys(request).Count;
        // Grob nach Umfang skaliert (statt fixem Wert) - eine volle Woche mit 3 Mahlzeiten/Tag braucht
        // deutlich mehr Tokens als ein 2-Tage-Plan, sonst droht die Antwort bei größeren Plänen
        // mitten in der Zutatenliste abgeschnitten zu werden.
        // Pro Mahlzeit kommt neben Rezept und Zutaten jetzt auch die Abwiege-Liste hinzu (daher 800 statt 600).
        var maxTokens = Math.Clamp(1500 + request.DayCount * mealCount * 800, 2000, 20000);

        Message response;
        var stopwatch = Stopwatch.StartNew();
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
        catch (Exception ex) when (ex is TaskCanceledException or TimeoutException && !cancellationToken.IsCancellationRequested)
        {
            // Das SDK meldet das eigene Zeitlimit als TaskCanceledException (nicht als AnthropicIOException).
            _logger.LogError(ex, "Zeitlimit bei Speiseplan-Generierung nach {Seconds:0} s überschritten (Modell {Model}, MaxTokens {MaxTokens}).",
                stopwatch.Elapsed.TotalSeconds, options.Model, maxTokens);
            throw new InvalidOperationException(
                $"Die KI hat nicht innerhalb von {GenerationTimeout.TotalMinutes:0} Minuten geantwortet. Bitte erneut versuchen.");
        }
        catch (AnthropicIOException ex)
        {
            // Verbindungsabbruch (kein AnthropicApiException) - als verständliche Meldung statt
            // unbehandelter Ausnahme, die in Blazor Server die ganze Sitzung beendet.
            _logger.LogError(ex, "Verbindungsfehler zur Anthropic-API bei Speiseplan-Generierung nach {Seconds:0} s.", stopwatch.Elapsed.TotalSeconds);
            throw new InvalidOperationException("Die Verbindung zur KI ist fehlgeschlagen. Bitte erneut versuchen.");
        }

        // Dauer und Tokenzahl protokollieren - macht sichtbar, wie schnell das gewählte Modell wirklich antwortet.
        _logger.LogInformation(
            "Speiseplan-Antwort nach {Seconds:0.0} s: Modell {Model}, {Days} Tage x {Meals} Mahlzeiten, stop_reason {StopReason}, {OutputTokens} Output-Tokens (MaxTokens {MaxTokens}).",
            stopwatch.Elapsed.TotalSeconds, options.Model, request.DayCount, mealCount, response.StopReason, response.Usage?.OutputTokens, maxTokens);

        if (response.StopReason == "refusal")
        {
            throw new InvalidOperationException("Die KI hat die Generierung dieses Speiseplans abgelehnt.");
        }

        if (response.StopReason == "max_tokens")
        {
            // Konkrete Zahlen in der Meldung: bei nur wenigen Mahlzeiten ist das Limit nie erreichbar, wenn die
            // KI normal antwortet - dann liegt es am Modell (z. B. langes "Nachdenken" vor der Antwort).
            throw new InvalidOperationException(
                $"Die Antwort der KI wurde nach {response.Usage?.OutputTokens} Tokens abgeschnitten (Limit {maxTokens}, " +
                $"Modell {options.Model}, Dauer {stopwatch.Elapsed.TotalSeconds:0} s). Bitte erneut generieren " +
                "oder weniger Tage wählen.");
        }

        var jsonText = response.Content.Select(b => b.Value).OfType<TextBlock>().FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            throw new InvalidOperationException("Die KI hat keine auswertbare Antwort geliefert.");
        }

        MealPlan plan;
        try
        {
            plan = ParseMealPlan(request, jsonText);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
        {
            _logger.LogError(ex, "Speiseplan-Antwort der KI war nicht lesbar. Anfang der Antwort: {Snippet}",
                jsonText[..Math.Min(500, jsonText.Length)]);
            throw new InvalidOperationException($"Die Antwort der KI war nicht lesbar ({ex.GetType().Name}). Bitte erneut generieren.");
        }

        EnsureNoPlaceholders(plan);
        return plan;
    }

    private static readonly string[] PlaceholderMarkers = ["placeholder", "platzhalter", "tbd", "n/a", "siehe oben"];

    // Bei umfangreichen Plänen (viele Tage x Mahlzeiten) neigt die KI trotz expliziter Anweisung im
    // Prompt gelegentlich dazu, nach der ersten sorgfältig ausgearbeiteten Mahlzeit den Rest mit
    // Lückentext abzuspeisen. Lieber hier laut scheitern (Nutzer generiert neu) als eine erkennbar
    // unvollständige Antwort unbemerkt zu speichern.
    private static void EnsureNoPlaceholders(MealPlan plan)
    {
        var hasPlaceholder = plan.Meals.Any(meal =>
            string.IsNullOrWhiteSpace(meal.Title) ||
            string.IsNullOrWhiteSpace(meal.Description) ||
            PlaceholderMarkers.Any(marker =>
                meal.Title.Contains(marker, StringComparison.OrdinalIgnoreCase) ||
                meal.Description.Contains(marker, StringComparison.OrdinalIgnoreCase)));

        if (hasPlaceholder)
        {
            throw new InvalidOperationException(
                "Die KI hat einen unvollständigen Plan mit Platzhaltern statt echter Mahlzeiten geliefert. " +
                "Bitte einfach erneut generieren - das passiert vor allem bei sehr langen Plänen und klappt " +
                "beim zweiten Versuch meist.");
        }
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
            Du erstellst alltagstaugliche Wochenspeisepläne für einen {PlannedMeal.DefaultServings}-Personen-Haushalt in Deutschland.
            {focusText}
            Die Gerichte sollen mit normalem Aufwand nachkochbar sein und ihre Zutaten in einem
            durchschnittlichen deutschen Supermarkt (z. B. Rewe/Edeka) erhältlich sein. "description" ist
            eine kurze, stichpunktartige Zubereitungsanleitung auf Deutsch (3-6 Schritte). Mengenangaben
            in "ingredients" gelten für genau {PlannedMeal.DefaultServings} Portionen (eine Portion je Person) - gib
            realistische Mengen mit gängigen Einheiten an (g, kg, ml, l, Stück, Bund, Dose, Packung).
            "portionItems" ist die Abwiege-Liste für genau EINE Portion, damit sich die Kalorien beim
            Anrichten exakt zählen lassen: jede Komponente, die auf dem Teller landet, als eigener Eintrag
            mit Name, Gewicht in Gramm im ZUBEREITETEN, verzehrfertigen Zustand (z. B. "Pellkartoffeln,
            gekocht" 200 g; "Lachsfilet, gebraten" 120 g; "Olivenöl" 10 g - nicht das Rohgewicht der
            Einkaufsmenge) und den Kilokalorien genau dieser Menge. Öl, Dressing, Aufstrich, Soße und
            Toppings gehören als eigene Einträge dazu, sie machen oft viele Kalorien aus. Wird das Gericht
            als Ganzes angerichtet (Auflauf, Eintopf, Suppe, Porridge, Salat mit Dressing), genügt EIN
            Eintrag für die gesamte Portion in Gramm (bei Flüssigkeiten gilt ml ≈ g). "caloriesPerServing"
            ist die Summe der Kalorien aller Einträge einer Portion als ganze Zahl (z. B. 480) - nicht die
            Summe für alle Portionen. Ist eine Zutat nicht sinnvoll
            zu bemessen (z. B. "Salz nach Geschmack"), setze "quantity" auf 0 und "unit" auf einen leeren
            String "". Wähle "category"
            IMMER aus der vorgegebenen Liste passend zur Zutat. Wiederhole Zutaten über mehrere Mahlzeiten
            hinweg bewusst (z. B. dieselbe Gemüsesorte), um Einkauf und Reste sinnvoll zu halten, statt für
            jede Mahlzeit komplett neue Zutaten zu erfinden.

            WICHTIG - das Antwortschema hat für JEDEN Tag (Schlüssel "day0", "day1", ...) und JEDE
            angeforderte Mahlzeit (Schlüssel "fruehstueck"/"mittag"/"abend") ein Pflichtfeld. Das ist
            KEINE Vorlage, bei der nur das erste Beispiel ausgearbeitet und der Rest knapp abgehandelt
            werden soll - jede einzelne Mahlzeit über den gesamten Zeitraum verdient dieselbe Sorgfalt
            wie die allererste: ein echtes, konkretes, individuelles Gericht mit vollständiger, sinnvoller
            Beschreibung und realistischer Zutatenliste. Verwende NIEMALS Platzhalter- oder Lückentext
            wie "placeholder", "TBD", "siehe oben", "wie Tag 1" oder leere/generische Angaben, auch nicht
            bei späteren Tagen oder wenn die Antwort dadurch länger wird - eine unvollständige Antwort ist
            nutzlos für den Nutzer. Plane lieber weniger abwechslungsreich (z. B. dieselbe Mahlzeit an zwei
            Tagen wiederholen), als irgendein Feld mit Platzhaltertext zu füllen.
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

        var notes = KuecheSettingsService.Normalize(request.Notes);
        if (notes is not null)
        {
            prompt += $" Zusätzliche Wünsche/Ausschlüsse des Nutzers: {notes}";
        }

        prompt += $" Bitte alle {request.DayCount} Tage mit vollständig ausgearbeiteten, echten Mahlzeiten " +
            "füllen - nicht nur den ersten Tag, keine Platzhalter danach.";

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

                var portionItems = new List<MealPortionItem>();
                if (mealElement.TryGetProperty("portionItems", out var portionItemsElement) &&
                    portionItemsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var itemElement in portionItemsElement.EnumerateArray())
                    {
                        var itemName = itemElement.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                        var grams = itemElement.TryGetProperty("grams", out var gramsElement) && gramsElement.TryGetInt32(out var parsedGrams) ? parsedGrams : 0;
                        var itemCalories = itemElement.TryGetProperty("calories", out var itemCaloriesElement) && itemCaloriesElement.TryGetInt32(out var parsedItemCalories) ? parsedItemCalories : 0;

                        if (string.IsNullOrWhiteSpace(itemName) || grams <= 0 || itemCalories < 0)
                        {
                            continue;
                        }

                        portionItems.Add(new MealPortionItem { Name = itemName.Trim(), Grams = grams, Calories = itemCalories });
                    }
                }

                // Negative oder unplausibel große Werte als "unbekannt" (0) behandeln statt sie anzuzeigen.
                var calories = mealElement.TryGetProperty("caloriesPerServing", out var caloriesElement) &&
                               caloriesElement.TryGetInt32(out var parsedCalories) &&
                               parsedCalories is > 0 and <= 5000
                    ? parsedCalories
                    : 0;

                // Die Summe der Einzelkomponenten ist die Quelle der Wahrheit, damit die angezeigte Gesamtzahl
                // zu dem passt, was tatsächlich abgewogen wird (die KI summiert ihre eigenen Werte nicht
                // immer exakt).
                var itemsTotal = portionItems.Sum(i => i.Calories);
                if (itemsTotal is > 0 and <= 5000)
                {
                    calories = itemsTotal;
                }

                plan.Meals.Add(new PlannedMeal
                {
                    Date = date,
                    MealType = mealType,
                    Title = mealElement.TryGetProperty("title", out var titleElement) ? titleElement.GetString() ?? string.Empty : string.Empty,
                    Description = mealElement.TryGetProperty("description", out var descriptionElement) ? descriptionElement.GetString() ?? string.Empty : string.Empty,
                    Servings = PlannedMeal.DefaultServings,
                    CaloriesPerServing = calories,
                    PortionItems = portionItems,
                    Ingredients = ingredients,
                });
            }
        }

        return plan;
    }
}
