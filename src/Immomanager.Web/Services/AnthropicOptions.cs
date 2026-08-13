namespace Immomanager.Web.Services;

/// <summary>Bindung für den "Anthropic"-Abschnitt in appsettings.json (ApiKey per Umgebungsvariable
/// Anthropic__ApiKey überschreibbar, damit der Schlüssel nie im Repository landen muss).</summary>
public class AnthropicOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "claude-opus-4-8";
}
