using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Immomanager.Web.Models;
using Microsoft.Extensions.Options;

namespace Immomanager.Web.Services;

/// <summary>Analysiert extrahierten Exposé-Text über die Anthropic Claude API und liefert strukturierte
/// Ankaufsprüfungs-Daten. Nutzt "Structured Outputs" (JSON-Schema), damit die Antwort garantiert
/// valides, zum Schema passendes JSON ist statt auf Prompt-Disziplin angewiesen zu sein.</summary>
public class AnthropicExposeAnalysisService : IExposeAnalysisService
{
    private const int MaxExposeTextLength = 15000;

    private const string SystemPrompt = """
        Du analysierst Texte aus Immobilien-Exposés (aus PDF extrahiert) und extrahierst strukturierte
        Daten für eine Ankaufsprüfung. Fülle nur Felder, für die im Text tatsächlich eine Information
        vorhanden ist - ist eine Information nicht vorhanden, setze den Wert auf null. Errate nichts.
        Gib Zahlen ohne Tausendertrennzeichen, ohne Währungssymbole und ohne Einheiten an.
        "summary" ist eine kurze, stichpunktartige Zusammenfassung der wichtigsten Highlights des
        Objekts in 3-4 Sätzen auf Deutsch.
        """;

    private static readonly Dictionary<string, JsonElement> ResponseSchema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
        {
          "type": "object",
          "properties": {
            "objectName": { "type": ["string", "null"] },
            "street": { "type": ["string", "null"] },
            "postalCode": { "type": ["string", "null"] },
            "city": { "type": ["string", "null"] },
            "purchasePrice": { "type": ["number", "null"] },
            "livingAreaSqm": { "type": ["number", "null"] },
            "yearBuilt": { "type": ["integer", "null"] },
            "unitCount": { "type": ["integer", "null"] },
            "parkingSpaces": { "type": ["integer", "null"] },
            "netColdRentMonthly": { "type": ["number", "null"] },
            "nonAllocableCostsMonthly": { "type": ["number", "null"] },
            "maintenanceReserveInHousingFee": { "type": ["number", "null"] },
            "summary": { "type": ["string", "null"] }
          },
          "required": ["objectName", "street", "postalCode", "city", "purchasePrice", "livingAreaSqm", "yearBuilt", "unitCount", "parkingSpaces", "netColdRentMonthly", "nonAllocableCostsMonthly", "maintenanceReserveInHousingFee", "summary"],
          "additionalProperties": false
        }
        """)!;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IOptionsMonitor<AnthropicOptions> _optionsMonitor;
    private readonly ILogger<AnthropicExposeAnalysisService> _logger;

    public AnthropicExposeAnalysisService(IOptionsMonitor<AnthropicOptions> optionsMonitor, ILogger<AnthropicExposeAnalysisService> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    // Bewusst IOptionsMonitor statt IOptions: liest bei jedem Aufruf den aktuellen Stand, damit ein
    // über die Einstellungen-Seite gespeicherter Key sofort wirkt, ohne die App neu zu starten.
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_optionsMonitor.CurrentValue.ApiKey);

    public async Task<ExposeAnalysisResult> AnalyzeAsync(string exposeText, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Kein Anthropic API-Key hinterlegt. Bitte unter \"Einstellungen\" in der Navigation konfigurieren.");
        }

        var options = _optionsMonitor.CurrentValue;
        var truncatedText = exposeText.Length > MaxExposeTextLength ? exposeText[..MaxExposeTextLength] : exposeText;

        AnthropicClient client = new() { ApiKey = options.ApiKey };

        Message response;
        try
        {
            response = await client.Messages.Create(new MessageCreateParams
            {
                Model = options.Model,
                MaxTokens = 2000,
                System = SystemPrompt,
                OutputConfig = new OutputConfig
                {
                    Format = new JsonOutputFormat { Schema = ResponseSchema },
                },
                Messages = [new() { Role = Role.User, Content = $"Text des Exposés:\n\"\"\"\n{truncatedText}\n\"\"\"" }],
            }, cancellationToken);
        }
        catch (AnthropicUnauthorizedException ex)
        {
            _logger.LogError(ex, "Anthropic API-Key ungültig bei Exposé-Analyse.");
            throw new InvalidOperationException("Der Anthropic API-Key ist ungültig oder abgelaufen.");
        }
        catch (AnthropicRateLimitException ex)
        {
            _logger.LogWarning(ex, "Anthropic-Anfragelimit bei Exposé-Analyse erreicht.");
            throw new InvalidOperationException("Anthropic-Anfragelimit erreicht - bitte kurz warten und erneut versuchen.");
        }
        catch (AnthropicApiException ex)
        {
            _logger.LogError(ex, "Anthropic-API-Fehler bei Exposé-Analyse.");
            throw new InvalidOperationException($"Fehler bei der KI-Analyse: {ex.Message}");
        }

        if (response.StopReason == "refusal")
        {
            throw new InvalidOperationException("Die KI hat die Analyse dieses Dokuments abgelehnt.");
        }

        var jsonText = response.Content.Select(b => b.Value).OfType<TextBlock>().FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            throw new InvalidOperationException("Die KI hat keine auswertbare Antwort geliefert.");
        }

        return JsonSerializer.Deserialize<ExposeAnalysisResult>(jsonText, JsonOptions)
            ?? throw new InvalidOperationException("Die KI-Antwort konnte nicht gelesen werden.");
    }
}
