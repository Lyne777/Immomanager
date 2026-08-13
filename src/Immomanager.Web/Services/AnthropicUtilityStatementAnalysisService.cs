using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Immomanager.Web.Models;
using Microsoft.Extensions.Options;

namespace Immomanager.Web.Services;

/// <summary>Analysiert extrahierten Text einer Nebenkosten-/Betriebskostenabrechnung über die
/// Anthropic Claude API: extrahiert Abrechnungsjahr, Gesamtsumme und alle Kostenpositionen gemäß
/// BetrKV. Nutzt "Structured Outputs" wie die Exposé- und Policen-Analyse.</summary>
public class AnthropicUtilityStatementAnalysisService : IUtilityStatementAnalysisService
{
    private const int MaxStatementTextLength = 15000;

    private const string SystemPrompt = """
        Du analysierst Texte aus Nebenkosten-/Betriebskostenabrechnungen (aus PDF extrahiert, oft von
        Hausverwaltungen) für ein Immobilien-Portfolio. Extrahiere das Abrechnungsjahr und die
        ausgewiesene Gesamtsumme aller Betriebskosten für das Gesamtgebäude (nicht die auf einzelne
        Mieter umgelegten Anteile). Fülle nur Felder, für die im Text tatsächlich eine Information
        vorhanden ist, sonst null. Errate nichts. Gib Zahlen ohne Tausendertrennzeichen, ohne
        Währungssymbole an.

        Extrahiere außerdem jede einzelne Kostenposition der Abrechnung in "costItems". Ordne jede
        Position genau einer dieser Kategorien zu (exakter Schlüssel, keine eigenen Bezeichnungen):
        HeizungWarmwasser, Grundsteuer, Gebaeudeversicherung, Muellabfuhr, Allgemeinstrom,
        WasserAbwasser, Hausreinigung, Gartenpflege, Schornsteinfeger, SonstigeBetrKV (Sammelkategorie
        für alles, was in keine der anderen Kategorien passt). "description" ist die Originalbezeichnung
        aus der Abrechnung (z. B. "Gemeindesteuern Stadt Siershahn" oder "Stadtwerke Müll"), "amount"
        der zugehörige Betrag für das Gesamtgebäude.

        "summary" ist eine kurze, stichpunktartige Zusammenfassung in 2-3 Sätzen auf Deutsch, welche
        Kostenart den größten Anteil ausmacht und ob etwas auffällig hoch erscheint.
        """;

    private static readonly Dictionary<string, JsonElement> ResponseSchema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
        {
          "type": "object",
          "properties": {
            "year": { "type": ["integer", "null"] },
            "totalCosts": { "type": ["number", "null"] },
            "summary": { "type": ["string", "null"] },
            "costItems": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "category": {
                    "type": "string",
                    "enum": ["HeizungWarmwasser", "Grundsteuer", "Gebaeudeversicherung", "Muellabfuhr", "Allgemeinstrom", "WasserAbwasser", "Hausreinigung", "Gartenpflege", "Schornsteinfeger", "SonstigeBetrKV"]
                  },
                  "description": { "type": "string" },
                  "amount": { "type": "number" }
                },
                "required": ["category", "description", "amount"],
                "additionalProperties": false
              }
            }
          },
          "required": ["year", "totalCosts", "summary", "costItems"],
          "additionalProperties": false
        }
        """)!;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IOptionsMonitor<AnthropicOptions> _optionsMonitor;
    private readonly ILogger<AnthropicUtilityStatementAnalysisService> _logger;

    public AnthropicUtilityStatementAnalysisService(IOptionsMonitor<AnthropicOptions> optionsMonitor, ILogger<AnthropicUtilityStatementAnalysisService> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_optionsMonitor.CurrentValue.ApiKey);

    public async Task<UtilityStatementAnalysisResult> AnalyzeAsync(string statementText, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Kein Anthropic API-Key hinterlegt. Bitte unter \"Einstellungen\" in der Navigation konfigurieren.");
        }

        var options = _optionsMonitor.CurrentValue;
        var truncatedText = statementText.Length > MaxStatementTextLength ? statementText[..MaxStatementTextLength] : statementText;

        AnthropicClient client = new() { ApiKey = options.ApiKey };

        Message response;
        try
        {
            response = await client.Messages.Create(new MessageCreateParams
            {
                Model = options.Model,
                MaxTokens = 3000,
                System = SystemPrompt,
                OutputConfig = new OutputConfig
                {
                    Format = new JsonOutputFormat { Schema = ResponseSchema },
                },
                Messages = [new() { Role = Role.User, Content = $"Text der Nebenkostenabrechnung:\n\"\"\"\n{truncatedText}\n\"\"\"" }],
            }, cancellationToken);
        }
        catch (AnthropicUnauthorizedException ex)
        {
            _logger.LogError(ex, "Anthropic API-Key ungültig bei Nebenkosten-Analyse.");
            throw new InvalidOperationException("Der Anthropic API-Key ist ungültig oder abgelaufen.");
        }
        catch (AnthropicRateLimitException ex)
        {
            _logger.LogWarning(ex, "Anthropic-Anfragelimit bei Nebenkosten-Analyse erreicht.");
            throw new InvalidOperationException("Anthropic-Anfragelimit erreicht - bitte kurz warten und erneut versuchen.");
        }
        catch (AnthropicApiException ex)
        {
            _logger.LogError(ex, "Anthropic-API-Fehler bei Nebenkosten-Analyse.");
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

        return JsonSerializer.Deserialize<UtilityStatementAnalysisResult>(jsonText, JsonOptions)
            ?? throw new InvalidOperationException("Die KI-Antwort konnte nicht gelesen werden.");
    }
}
