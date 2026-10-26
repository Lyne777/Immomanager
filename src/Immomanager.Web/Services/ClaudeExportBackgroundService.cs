using Microsoft.Extensions.Options;

namespace Immomanager.Web.Services;

/// <summary>Stößt den Export für Claude täglich zur konfigurierten Uhrzeit an (Standard 03:00 Uhr,
/// Europe/Berlin). Der Container läuft ohnehin dauerhaft, daher reicht ein eigener Timer - kein Cron auf
/// der NAS nötig. Nach einem Neustart wird der Export nachgeholt, falls der letzte erfolgreiche Lauf
/// älter als 24 Stunden ist (z. B. weil die NAS zur geplanten Zeit aus war).</summary>
public class ClaudeExportBackgroundService : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ClaudeExportState _state;
    private readonly IOptions<ClaudeExportOptions> _options;
    private readonly ILogger<ClaudeExportBackgroundService> _logger;

    public ClaudeExportBackgroundService(
        IServiceScopeFactory scopeFactory,
        ClaudeExportState state,
        IOptions<ClaudeExportOptions> options,
        ILogger<ClaudeExportBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _state = state;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            _logger.LogInformation("Täglicher Export für Claude ist deaktiviert (ClaudeExport:Enabled=false).");
            return;
        }

        try
        {
            // Kurze Verzögerung, damit Migration/Start-Backup und die ersten Seitenaufrufe nicht
            // gleichzeitig mit dem Export um die Datenbank konkurrieren.
            await Task.Delay(StartupDelay, stoppingToken);

            if (_state.LastSuccessUtc is not { } last || DateTime.UtcNow - last > MaxAge)
            {
                await RunExportAsync(stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = TimeUntilNextRun(options.Hour);
                _logger.LogInformation("Nächster Export für Claude in {Delay:hh\\:mm}.", delay);
                await Task.Delay(delay, stoppingToken);
                await RunExportAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normales Herunterfahren.
        }
    }

    private async Task RunExportAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var exportService = scope.ServiceProvider.GetRequiredService<IClaudeExportService>();
        await exportService.ExportAsync(cancellationToken);
    }

    private static TimeSpan TimeUntilNextRun(int hour)
    {
        var zone = ClaudeExportState.ResolveTimeZone();
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime;
        var next = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, Math.Clamp(hour, 0, 23), 0, 0);
        if (next <= nowLocal)
        {
            next = next.AddDays(1);
        }

        // Nicht existierende lokale Uhrzeit (Sommerzeit-Umstellung) um eine Stunde nach hinten schieben.
        if (zone.IsInvalidTime(next))
        {
            next = next.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(next, zone) - DateTime.UtcNow;
    }
}
