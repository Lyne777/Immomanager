using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Immomanager.Web.Models;
using Microsoft.Extensions.Options;

namespace Immomanager.Web.Services;

/// <summary>Nutzt die von diversen Open-Source-Projekten reverse-engineerte Bring!-API
/// (siehe https://github.com/miaucl/bring-api) - kein offizieller Endpunkt, keine SLA/Garantie.
/// Bewusst ohne Token-Caching: jeder Aufruf loggt sich frisch ein (Nutzung ist selten - höchstens
/// ein paar Mal pro Woche -, das spart die Komplexität einer Refresh-Token-Verwaltung).</summary>
public class BringService : IBringService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<BringOptions> _optionsMonitor;
    private readonly ILogger<BringService> _logger;

    public BringService(IHttpClientFactory httpClientFactory, IOptionsMonitor<BringOptions> optionsMonitor, ILogger<BringService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<List<BringList>> GetListsAsync(CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            throw new InvalidOperationException("Bitte zuerst Email und Passwort speichern.");
        }

        var client = _httpClientFactory.CreateClient(nameof(BringService));
        var session = await LoginAsync(client, options.Email, options.Password, cancellationToken);
        ApplyAuthHeaders(client, session);

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync($"bringusers/{session.Uuid}/lists", cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Bring!-Listen konnten nicht geladen werden.");
            throw new InvalidOperationException("Bring!-Listen konnten nicht geladen werden.");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
        var lists = new List<BringList>();
        if (json.TryGetProperty("lists", out var listsElement))
        {
            foreach (var item in listsElement.EnumerateArray())
            {
                lists.Add(new BringList(
                    item.GetProperty("listUuid").GetString() ?? string.Empty,
                    item.GetProperty("name").GetString() ?? "Unbenannte Liste"));
            }
        }

        return lists;
    }

    public async Task SendItemsAsync(IEnumerable<ShoppingListItem> items, CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            throw new InvalidOperationException("Keine Bring!-Zugangsdaten hinterlegt. Bitte unter \"Einstellungen\" konfigurieren.");
        }
        if (string.IsNullOrWhiteSpace(options.ListUuid))
        {
            throw new InvalidOperationException("Keine Bring!-Zielliste ausgewählt. Bitte unter \"Einstellungen\" auswählen.");
        }

        var client = _httpClientFactory.CreateClient(nameof(BringService));
        var session = await LoginAsync(client, options.Email, options.Password, cancellationToken);
        ApplyAuthHeaders(client, session);

        var changes = items.Select(item => new
        {
            itemId = item.Name,
            spec = FormatSpec(item.Quantity, item.Unit),
            uuid = Guid.NewGuid().ToString(),
            operation = "TO_PURCHASE",
        });

        var payload = new { changes, sender = "" };

        try
        {
            var response = await client.PutAsJsonAsync($"v2/bringlists/{options.ListUuid}/items", payload, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Einkaufsliste konnte nicht an Bring! gesendet werden.");
            throw new InvalidOperationException("Einkaufsliste konnte nicht an Bring! gesendet werden - die inoffizielle API hat sich ggf. geändert.");
        }
    }

    private async Task<BringSession> LoginAsync(HttpClient client, string email, string password, CancellationToken cancellationToken)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["email"] = email, ["password"] = password });

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync("v2/bringauth", form, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Bring!-Anmeldung fehlgeschlagen (Netzwerkfehler).");
            throw new InvalidOperationException("Bring!-Anmeldung fehlgeschlagen - keine Verbindung möglich.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Bring!-Anmeldung fehlgeschlagen - bitte Email/Passwort prüfen.");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
        return new BringSession(
            json.GetProperty("uuid").GetString() ?? throw new InvalidOperationException("Unerwartete Antwort von Bring!."),
            json.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("Unerwartete Antwort von Bring!."),
            json.GetProperty("token_type").GetString() ?? "Bearer");
    }

    private static void ApplyAuthHeaders(HttpClient client, BringSession session)
    {
        client.DefaultRequestHeaders.Remove("Authorization");
        client.DefaultRequestHeaders.Add("Authorization", $"{session.TokenType} {session.AccessToken}");
        client.DefaultRequestHeaders.Remove("X-BRING-USER-UUID");
        client.DefaultRequestHeaders.Add("X-BRING-USER-UUID", session.Uuid);
    }

    private static string FormatSpec(decimal? quantity, string? unit)
    {
        if (!quantity.HasValue)
        {
            return unit ?? string.Empty;
        }

        var quantityText = quantity.Value.ToString("0.##", Inv);
        return string.IsNullOrWhiteSpace(unit) ? quantityText : $"{quantityText} {unit}";
    }

    private sealed record BringSession(string Uuid, string AccessToken, string TokenType);
}
