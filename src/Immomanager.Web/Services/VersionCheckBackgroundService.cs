using System.Text.Json;

namespace Immomanager.Web.Services;

/// <summary>Prüft periodisch, ob auf dem main-Branch des (öffentlichen) GitHub-Repos ein neuerer
/// Commit vorliegt als die aktuell laufende, ins Image gebackene Version (Dockerfile: ARG GIT_SHA
/// -> ENV APP_VERSION, gesetzt vom Workflow via github.sha). Bewusste Vereinfachung: verglichen wird
/// der letzte main-Commit, nicht das tatsächlich veröffentlichte ghcr.io-Image direkt - ein
/// authentifizierter Zugriff auf die (bewusst privat gehaltene) Container Registry wäre dafür nötig
/// und würde den ganzen Sinn des öffentlichen Repos (Versions-Check ohne Token) zunichtemachen.
/// Schlägt der CI-Build für einen Push mal fehl, kann das kurzzeitig eine "Update verfügbar"-Meldung
/// zeigen, obwohl noch kein neues Image existiert - behebt sich von selbst mit dem nächsten
/// erfolgreichen Build.</summary>
public class VersionCheckBackgroundService : BackgroundService
{
    private const string RepositoryOwner = "Lyne777";
    private const string RepositoryName = "Immomanager";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly VersionCheckState _state;
    private readonly ILogger<VersionCheckBackgroundService> _logger;

    public VersionCheckBackgroundService(IHttpClientFactory httpClientFactory, VersionCheckState state, ILogger<VersionCheckBackgroundService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _state = state;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var currentVersion = Environment.GetEnvironmentVariable("APP_VERSION");

        // Ohne gebackene Version (z. B. lokale Entwicklung via "dotnet run") gibt es nichts, wogegen
        // sinnvoll verglichen werden könnte - Check dauerhaft überspringen statt mit "unknown" zu vergleichen.
        if (string.IsNullOrWhiteSpace(currentVersion) || currentVersion == "unknown")
        {
            _logger.LogInformation("Versions-Check übersprungen: keine APP_VERSION gesetzt (vermutlich lokale Entwicklung).");
            return;
        }

        _state.CurrentVersion = ShortSha(currentVersion);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForUpdateAsync(currentVersion, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Netzwerkfehler/Rate-Limit duerfen die App nicht beeintraechtigen - beim naechsten
                // Intervall einfach erneut versuchen.
                _logger.LogWarning(ex, "Versions-Check gegen GitHub fehlgeschlagen.");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CheckForUpdateAsync(string currentVersionFull, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(VersionCheckBackgroundService));
        var response = await client.GetAsync(
            $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/commits/main", cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var latestShaFull = document.RootElement.GetProperty("sha").GetString();

        if (string.IsNullOrWhiteSpace(latestShaFull))
        {
            return;
        }

        _state.LatestVersion = ShortSha(latestShaFull);
        _state.IsUpdateAvailable = !string.Equals(latestShaFull, currentVersionFull, StringComparison.OrdinalIgnoreCase);
        _state.LastCheckedUtc = DateTime.UtcNow;
    }

    private static string ShortSha(string sha) => sha.Length > 7 ? sha[..7] : sha;
}
