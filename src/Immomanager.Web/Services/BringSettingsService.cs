using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Immomanager.Web.Services;

public class BringSettingsService : IBringSettingsService
{
    private readonly IOptionsMonitor<BringOptions> _optionsMonitor;
    private readonly IConfiguration _configuration;
    private readonly string _settingsFilePath;

    public BringSettingsService(StorageOptions storageOptions, IOptionsMonitor<BringOptions> optionsMonitor, IConfiguration configuration)
    {
        _optionsMonitor = optionsMonitor;
        _configuration = configuration;
        _settingsFilePath = Path.Combine(storageOptions.DataDirectoryAbsolute, "bring-settings.json");
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_optionsMonitor.CurrentValue.Email) &&
        !string.IsNullOrWhiteSpace(_optionsMonitor.CurrentValue.Password);

    public bool HasListSelected => !string.IsNullOrWhiteSpace(_optionsMonitor.CurrentValue.ListUuid);

    public string? CurrentEmail => _optionsMonitor.CurrentValue.Email;

    public string? CurrentListName => _optionsMonitor.CurrentValue.ListName;

    public async Task SaveCredentialsAsync(string email, string? password)
    {
        var current = _optionsMonitor.CurrentValue;
        var effectivePassword = string.IsNullOrWhiteSpace(password) ? current.Password : password.Trim();

        // Ein Wechsel der Email invalidiert eine ggf. zuvor gewählte Liste (die gehört zum alten Konto).
        var listUuid = email.Trim().Equals(current.Email, StringComparison.OrdinalIgnoreCase) ? current.ListUuid : null;
        var listName = email.Trim().Equals(current.Email, StringComparison.OrdinalIgnoreCase) ? current.ListName : null;

        await WriteAsync(new BringOptions { Email = email.Trim(), Password = effectivePassword, ListUuid = listUuid, ListName = listName });
    }

    public async Task SaveSelectedListAsync(string listUuid, string listName)
    {
        var current = _optionsMonitor.CurrentValue;
        await WriteAsync(new BringOptions { Email = current.Email, Password = current.Password, ListUuid = listUuid, ListName = listName });
    }

    private async Task WriteAsync(BringOptions options)
    {
        var wrapper = new Dictionary<string, BringOptions> { ["Bring"] = options };
        var json = JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_settingsFilePath, json);

        if (_configuration is IConfigurationRoot configurationRoot)
        {
            configurationRoot.Reload();
        }
    }
}
