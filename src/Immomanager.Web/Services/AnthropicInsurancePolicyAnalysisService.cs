using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Immomanager.Web.Models;
using Microsoft.Extensions.Options;

namespace Immomanager.Web.Services;

/// <summary>Analysiert extrahierten Text einer Versicherungspolice (Gebäude/Haftpflicht) über die
/// Anthropic Claude API: extrahiert Vertragsfakten (Anbieter, Scheinnummer, Beitrag, Laufzeit) und
/// gleicht die Police mit den Prüfpunkten der jeweiligen Kategorie ab. Nutzt "Structured Outputs"
/// wie die Exposé-Analyse, damit die Antwort garantiert valides JSON ist.</summary>
public class AnthropicInsurancePolicyAnalysisService : IInsurancePolicyAnalysisService
{
    private const int MaxPolicyTextLength = 15000;

    private const string SystemPrompt = """
        Du analysierst Texte aus Versicherungspolicen (aus PDF extrahiert) für ein Immobilien-Portfolio.
        Extrahiere die Vertragsfakten (Versicherungsgesellschaft, Versicherungsscheinnummer, Jahresbeitrag,
        Vertragsbeginn, Ablauf-/Verlängerungsdatum) - fülle nur Felder, für die im Text tatsächlich eine
        Information vorhanden ist, sonst null. Errate nichts. Gib Zahlen ohne Tausendertrennzeichen, ohne
        Währungssymbole an. Gib Datumsangaben so aus, wie sie im Text stehen (Freitext, z. B. "01.01.2024").

        Zusätzlich bekommst du eine Liste von Prüfpunkten mit Key und Frage. Gleiche für JEDEN dieser
        Prüfpunkte die Vertragsbedingungen der Police ab und liefere in "checkFindings" pro Prüfpunkt genau
        einen Eintrag mit demselben Key: "covered" = true, wenn die Police diesen Punkt eindeutig abdeckt,
        false, wenn die Police diesen Punkt eindeutig NICHT abdeckt oder ausschließt, null, wenn sich das
        aus dem Text nicht sicher beurteilen lässt. "note" ist eine sehr kurze Begründung/Fundstelle
        (z. B. "Elementarschäden explizit ausgeschlossen, siehe § 3") oder null, wenn nicht nötig.

        "summary" ist eine kurze, stichpunktartige Zusammenfassung der wichtigsten Eckdaten und
        auffälligsten Lücken in 2-4 Sätzen auf Deutsch.
        """;

    private static readonly Dictionary<string, JsonElement> ResponseSchema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
        {
          "type": "object",
          "properties": {
            "provider": { "type": ["string", "null"] },
            "policyNumber": { "type": ["string", "null"] },
            "annualPremium": { "type": ["number", "null"] },
            "startDate": { "type": ["string", "null"] },
            "expirationDate": { "type": ["string", "null"] },
            "summary": { "type": ["string", "null"] },
            "checkFindings": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "key": { "type": "string" },
                  "covered": { "type": ["boolean", "null"] },
                  "note": { "type": ["string", "null"] }
                },
                "required": ["key", "covered", "note"],
                "additionalProperties": false
              }
            }
          },
          "required": ["provider", "policyNumber", "annualPremium", "startDate", "expirationDate", "summary", "checkFindings"],
          "additionalProperties": false
        }
        """)!;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly AnthropicOptions _options;
    private readonly ILogger<AnthropicInsurancePolicyAnalysisService> _logger;

    public AnthropicInsurancePolicyAnalysisService(IOptions<AnthropicOptions> options, ILogger<AnthropicInsurancePolicyAnalysisService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<InsurancePolicyAnalysisResult> AnalyzeAsync(string policyText, InsuranceCategory category, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Kein Anthropic API-Key hinterlegt. Bitte in appsettings.json unter \"Anthropic:ApiKey\" " +
                "oder über die Umgebungsvariable Anthropic__ApiKey konfigurieren.");
        }

        var truncatedText = policyText.Length > MaxPolicyTextLength ? policyText[..MaxPolicyTextLength] : policyText;

        var checkPointsList = string.Join("\n", InsuranceCheckCatalog.Items
            .Where(i => i.Category == category)
            .Select(i => $"- Key \"{i.Key}\": {i.Title}"));

        var userMessage = $"""
            Kategorie: {InsuranceService.CategoryDisplayNames[category]}

            Zu prüfende Punkte (liefere für jeden genau einen Eintrag in "checkFindings" mit demselben Key):
            {checkPointsList}

            Text der Police:
            \"\"\"
            {truncatedText}
            \"\"\"
            """;

        AnthropicClient client = new() { ApiKey = _options.ApiKey };

        Message response;
        try
        {
            response = await client.Messages.Create(new MessageCreateParams
            {
                Model = _options.Model,
                MaxTokens = 3000,
                System = SystemPrompt,
                OutputConfig = new OutputConfig
                {
                    Format = new JsonOutputFormat { Schema = ResponseSchema },
                },
                Messages = [new() { Role = Role.User, Content = userMessage }],
            }, cancellationToken);
        }
        catch (AnthropicUnauthorizedException ex)
        {
            _logger.LogError(ex, "Anthropic API-Key ungültig bei Policen-Analyse.");
            throw new InvalidOperationException("Der Anthropic API-Key ist ungültig oder abgelaufen.");
        }
        catch (AnthropicRateLimitException ex)
        {
            _logger.LogWarning(ex, "Anthropic-Anfragelimit bei Policen-Analyse erreicht.");
            throw new InvalidOperationException("Anthropic-Anfragelimit erreicht - bitte kurz warten und erneut versuchen.");
        }
        catch (AnthropicApiException ex)
        {
            _logger.LogError(ex, "Anthropic-API-Fehler bei Policen-Analyse.");
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

        return JsonSerializer.Deserialize<InsurancePolicyAnalysisResult>(jsonText, JsonOptions)
            ?? throw new InvalidOperationException("Die KI-Antwort konnte nicht gelesen werden.");
    }
}
