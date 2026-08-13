using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Immomanager.Web.Models;
using Microsoft.Extensions.Options;

namespace Immomanager.Web.Services;

/// <summary>Analysiert extrahierten Text eines Mietvertrags über die Anthropic Claude API: Mieter,
/// Kontaktdaten, Mietbeginn/-ende, Kaltmiete, Nebenkostenvorauszahlung und Kaution. Nutzt "Structured
/// Outputs" wie die Exposé-, Policen- und Nebenkosten-Analyse.</summary>
public class AnthropicLeaseAnalysisService : ILeaseAnalysisService
{
    private const int MaxLeaseTextLength = 15000;

    private const string SystemPrompt = """
        Du analysierst Texte aus Wohnraum-Mietverträgen (aus PDF extrahiert) für ein
        Immobilien-Portfolio. Extrahiere den vollständigen Namen des Mieters (bei mehreren Mietern
        alle Namen kommagetrennt), E-Mail und Telefon (falls im Vertrag angegeben - oft fehlt das),
        Mietbeginn, ein etwaiges festes Mietende (bei unbefristeten Verträgen null lassen), die
        vereinbarte Nettokaltmiete pro Monat, die Nebenkostenvorauszahlung pro Monat und die Kaution.
        Fülle nur Felder, für die im Text tatsächlich eine Information vorhanden ist, sonst null.
        Errate nichts. Gib Zahlen ohne Tausendertrennzeichen, ohne Währungssymbole an.

        "summary" ist eine kurze, stichpunktartige Zusammenfassung in 2-3 Sätzen auf Deutsch:
        Vertragsart (befristet/unbefristet), Besonderheiten (z. B. Staffelmiete, Indexmiete,
        Haustierklausel, Kündigungsfristen), falls im Text erkennbar.
        """;

    private static readonly Dictionary<string, JsonElement> ResponseSchema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
        {
          "type": "object",
          "properties": {
            "tenantName": { "type": ["string", "null"] },
            "tenantEmail": { "type": ["string", "null"] },
            "tenantPhone": { "type": ["string", "null"] },
            "moveInDate": { "type": ["string", "null"] },
            "moveOutDate": { "type": ["string", "null"] },
            "coldRentMonthly": { "type": ["number", "null"] },
            "advancePaymentMonthly": { "type": ["number", "null"] },
            "securityDeposit": { "type": ["number", "null"] },
            "summary": { "type": ["string", "null"] }
          },
          "required": ["tenantName", "tenantEmail", "tenantPhone", "moveInDate", "moveOutDate", "coldRentMonthly", "advancePaymentMonthly", "securityDeposit", "summary"],
          "additionalProperties": false
        }
        """)!;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IOptionsMonitor<AnthropicOptions> _optionsMonitor;
    private readonly ILogger<AnthropicLeaseAnalysisService> _logger;

    public AnthropicLeaseAnalysisService(IOptionsMonitor<AnthropicOptions> optionsMonitor, ILogger<AnthropicLeaseAnalysisService> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_optionsMonitor.CurrentValue.ApiKey);

    public async Task<LeaseAnalysisResult> AnalyzeAsync(string leaseText, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Kein Anthropic API-Key hinterlegt. Bitte unter \"Einstellungen\" in der Navigation konfigurieren.");
        }

        var options = _optionsMonitor.CurrentValue;
        var truncatedText = leaseText.Length > MaxLeaseTextLength ? leaseText[..MaxLeaseTextLength] : leaseText;

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
                Messages = [new() { Role = Role.User, Content = $"Text des Mietvertrags:\n\"\"\"\n{truncatedText}\n\"\"\"" }],
            }, cancellationToken);
        }
        catch (AnthropicUnauthorizedException ex)
        {
            _logger.LogError(ex, "Anthropic API-Key ungültig bei Mietvertrags-Analyse.");
            throw new InvalidOperationException("Der Anthropic API-Key ist ungültig oder abgelaufen.");
        }
        catch (AnthropicRateLimitException ex)
        {
            _logger.LogWarning(ex, "Anthropic-Anfragelimit bei Mietvertrags-Analyse erreicht.");
            throw new InvalidOperationException("Anthropic-Anfragelimit erreicht - bitte kurz warten und erneut versuchen.");
        }
        catch (AnthropicApiException ex)
        {
            _logger.LogError(ex, "Anthropic-API-Fehler bei Mietvertrags-Analyse.");
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

        return JsonSerializer.Deserialize<LeaseAnalysisResult>(jsonText, JsonOptions)
            ?? throw new InvalidOperationException("Die KI-Antwort konnte nicht gelesen werden.");
    }
}
