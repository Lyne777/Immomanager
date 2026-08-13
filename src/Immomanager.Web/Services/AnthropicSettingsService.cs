using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Immomanager.Web.Services;

public class AnthropicSettingsService : IAnthropicSettingsService
{
    private readonly IOptionsMonitor<AnthropicOptions> _optionsMonitor;
    private readonly IConfiguration _configuration;
    private readonly string _settingsFilePath;

    public AnthropicSettingsService(StorageOptions storageOptions, IOptionsMonitor<AnthropicOptions> optionsMonitor, IConfiguration configuration)
    {
        _optionsMonitor = optionsMonitor;
        _configuration = configuration;
        _settingsFilePath = Path.Combine(storageOptions.DataDirectoryAbsolute, "anthropic-settings.json");
    }

    public bool IsApiKeyConfigured => !string.IsNullOrWhiteSpace(_optionsMonitor.CurrentValue.ApiKey);

    public string CurrentModel => _optionsMonitor.CurrentValue.Model;

    public async Task SaveAsync(string? apiKey, string model)
    {
        var effectiveApiKey = string.IsNullOrWhiteSpace(apiKey) ? _optionsMonitor.CurrentValue.ApiKey : apiKey.Trim();

        // Verschachtelt unter "Anthropic", da diese Datei als zusätzliche Konfigurationsquelle
        // eingebunden ist (siehe Program.cs) und dieselbe Struktur wie appsettings.json braucht,
        // damit die Bindung an AnthropicOptions ("Anthropic"-Sektion) greift.
        var wrapper = new Dictionary<string, AnthropicOptions>
        {
            ["Anthropic"] = new AnthropicOptions { ApiKey = effectiveApiKey, Model = model },
        };

        var json = JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_settingsFilePath, json);

        // "reloadOnChange" auf der Konfigurationsquelle sollte das automatisch übernehmen, aber
        // Datei-Watcher können auf gemounteten Docker-Volumes unzuverlässig sein - daher zusätzlich
        // explizit neu laden, damit der neue Key garantiert sofort (ohne Programmneustart) wirkt.
        if (_configuration is IConfigurationRoot configurationRoot)
        {
            configurationRoot.Reload();
        }
    }
}
