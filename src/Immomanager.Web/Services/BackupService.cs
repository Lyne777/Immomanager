using System.Text.Json;
using Immomanager.Web.Models;
using Microsoft.Data.Sqlite;

namespace Immomanager.Web.Services;

/// <summary>Erstellt/verwaltet Sicherungen der SQLite-Datenbankdatei im Backup-Unterverzeichnis des
/// Datenverzeichnisses und führt Restores durch. Nutzt bewusst die SQLite-eigene Online-Backup-API
/// (<see cref="SqliteConnection.BackupDatabase"/>) statt eines rohen Datei-Kopierens, damit ein
/// Backup auch bei laufendem Betrieb konsistent ist. Automatische vs. manuelle Sicherungen werden
/// rein über das Dateinamens-Präfix ("auto_"/"manual_") unterschieden - beim Bereinigen alter
/// Sicherungen (<see cref="PruneAutoBackupsAsync"/>) werden dadurch garantiert nie manuelle
/// Sicherungen angetastet.</summary>
public class BackupService : IBackupService
{
    private const string AutoPrefix = "auto";
    private const string ManualPrefix = "manual";

    private static readonly byte[] SqliteHeaderMagic = "SQLite format 3\0"u8.ToArray();

    private readonly StorageOptions _storageOptions;
    private readonly ILogger<BackupService> _logger;
    private readonly string _settingsFilePath;

    public BackupService(StorageOptions storageOptions, ILogger<BackupService> logger)
    {
        _storageOptions = storageOptions;
        _logger = logger;
        _settingsFilePath = Path.Combine(_storageOptions.DataDirectoryAbsolute, "backup-settings.json");
    }

    public Task<List<BackupFileInfo>> GetBackupsAsync()
    {
        if (!Directory.Exists(_storageOptions.BackupsDirectoryAbsolute))
        {
            return Task.FromResult(new List<BackupFileInfo>());
        }

        var backups = Directory.GetFiles(_storageOptions.BackupsDirectoryAbsolute, "*.db")
            .Select(ToBackupFileInfo)
            .OrderByDescending(b => b.CreatedAtUtc)
            .ToList();

        return Task.FromResult(backups);
    }

    public Task<BackupFileInfo> CreateManualBackupAsync(CancellationToken cancellationToken = default) =>
        CreateBackupAsync(ManualPrefix, cancellationToken);

    public Task DeleteBackupAsync(string fileName)
    {
        var path = ResolveExistingBackupPath(fileName);
        File.Delete(path);
        _logger.LogInformation("Backup {FileName} gelöscht.", fileName);
        return Task.CompletedTask;
    }

    public async Task RestoreFromExistingAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var backupPath = ResolveExistingBackupPath(fileName);
        ValidateSqliteHeader(backupPath);

        // Erst in eine private Staging-Kopie sichern, BEVOR die Pre-Restore-Sicherheitskopie erstellt
        // und dabei alte Auto-Backups bereinigt werden - sonst könnte das Bereinigen ausgerechnet
        // "backupPath" selbst löschen, falls das die wiederherzustellende Sicherung eine ältere
        // automatische Sicherung ist, die durch die neue Pre-Restore-Kopie aus dem Limit fällt.
        var stagingPath = Path.Combine(_storageOptions.BackupsDirectoryAbsolute, $"staging_{Guid.NewGuid():N}.db");
        File.Copy(backupPath, stagingPath, overwrite: true);
        try
        {
            await CreatePreRestoreSafetyBackupAsync(cancellationToken);
            ApplyRestoreFile(stagingPath);
            _logger.LogWarning("Datenbank aus vorhandener Sicherung {FileName} wiederhergestellt.", fileName);
        }
        finally
        {
            if (File.Exists(stagingPath))
            {
                File.Delete(stagingPath);
            }
        }
    }

    public async Task RestoreFromUploadAsync(Stream uploadedStream, CancellationToken cancellationToken = default)
    {
        var stagingPath = Path.Combine(_storageOptions.BackupsDirectoryAbsolute, $"staging_{Guid.NewGuid():N}.db");
        try
        {
            await using (var fileStream = new FileStream(stagingPath, FileMode.Create, FileAccess.Write))
            {
                await uploadedStream.CopyToAsync(fileStream, cancellationToken);
            }

            ValidateSqliteHeader(stagingPath);
            await CreatePreRestoreSafetyBackupAsync(cancellationToken);
            ApplyRestoreFile(stagingPath);
            _logger.LogWarning("Datenbank aus hochgeladener Sicherungsdatei wiederhergestellt.");
        }
        finally
        {
            if (File.Exists(stagingPath))
            {
                File.Delete(stagingPath);
            }
        }
    }

    public async Task<BackupSettings> GetSettingsAsync()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new BackupSettings();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_settingsFilePath);
            return JsonSerializer.Deserialize<BackupSettings>(json) ?? new BackupSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogError(ex, "Backup-Einstellungen konnten nicht gelesen werden, verwende Standardwerte.");
            return new BackupSettings();
        }
    }

    public async Task SaveSettingsAsync(BackupSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_settingsFilePath, json);
    }

    public async Task RunStartupAutoBackupIfEnabledAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync();
        if (!settings.AutoBackupOnStartupEnabled)
        {
            return;
        }

        await CreateBackupAsync(AutoPrefix, cancellationToken);
        await PruneAutoBackupsAsync(settings.AutoBackupRetentionCount);
    }

    /// <summary>Löscht die ältesten automatischen Sicherungen, bis nur noch "keepCount" übrig sind.
    /// Manuelle Sicherungen (Präfix "manual_") werden hier nie berücksichtigt oder gelöscht.</summary>
    private async Task PruneAutoBackupsAsync(int keepCount)
    {
        var autoBackupsToDelete = (await GetBackupsAsync())
            .Where(b => b.IsAutomatic)
            .OrderByDescending(b => b.CreatedAtUtc)
            .Skip(Math.Max(0, keepCount));

        foreach (var backup in autoBackupsToDelete)
        {
            var path = Path.Combine(_storageOptions.BackupsDirectoryAbsolute, backup.FileName);
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.LogInformation("Alte automatische Sicherung {FileName} bereinigt.", backup.FileName);
            }
        }
    }

    private async Task CreatePreRestoreSafetyBackupAsync(CancellationToken cancellationToken)
    {
        // Sicherheitsnetz: vor jedem Restore wird der aktuelle (gleich überschriebene) Stand
        // automatisch gesichert, damit ein versehentliches/falsches Restore selbst wieder rückgängig
        // gemacht werden kann. Zählt als automatische Sicherung und unterliegt daher auch dem
        // Aufbewahrungslimit, damit wiederholte Restores nicht unbegrenzt Sicherungen anhäufen.
        await CreateBackupAsync(AutoPrefix, cancellationToken, "prerestore");
        var settings = await GetSettingsAsync();
        await PruneAutoBackupsAsync(settings.AutoBackupRetentionCount);
    }

    private async Task<BackupFileInfo> CreateBackupAsync(string prefix, CancellationToken cancellationToken, string? tag = null)
    {
        Directory.CreateDirectory(_storageOptions.BackupsDirectoryAbsolute);

        var tagSuffix = tag is null ? "" : $"_{tag}";
        var fileName = $"{prefix}{tagSuffix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db";
        var destinationPath = Path.Combine(_storageOptions.BackupsDirectoryAbsolute, fileName);

        await using var source = new SqliteConnection($"Data Source={_storageOptions.DatabaseFilePath}");
        await source.OpenAsync(cancellationToken);
        await using var destination = new SqliteConnection($"Data Source={destinationPath}");
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);

        _logger.LogInformation("Backup {FileName} erstellt.", fileName);
        return ToBackupFileInfo(destinationPath);
    }

    private void ApplyRestoreFile(string sourcePath)
    {
        // Microsoft.Data.Sqlite pooled Verbindungen (native sqlite3-Handles) auch nach dem Schließen
        // eines DbContext - ohne ClearAllPools() könnte die Quell- oder Zieldatei noch gesperrt sein
        // (z. B. weil die Quelle selbst kurz zuvor als Backup-Ziel über eine gepoolte Verbindung
        // beschrieben wurde). ClearAllPools() räumt das meistens aus, daher zusätzlich mit
        // nachsichtigen FileShare-Flags und ein paar Wiederholungsversuchen kopieren, um verbliebene
        // kurzzeitige Sperren (z. B. durch Virenscanner) robust zu überstehen.
        SqliteConnection.ClearAllPools();
        CopyWithRetry(sourcePath, _storageOptions.DatabaseFilePath);

        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var sidecarPath = _storageOptions.DatabaseFilePath + suffix;
            if (File.Exists(sidecarPath))
            {
                File.Delete(sidecarPath);
            }
        }
    }

    private static void CopyWithRetry(string sourcePath, string destinationPath)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                sourceStream.CopyTo(destinationStream);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(200);
            }
        }
    }

    private string ResolveExistingBackupPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
        {
            throw new InvalidOperationException("Ungültiger Backup-Dateiname.");
        }

        var path = Path.Combine(_storageOptions.BackupsDirectoryAbsolute, fileName);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Sicherung \"{fileName}\" wurde nicht gefunden.");
        }

        return path;
    }

    private static void ValidateSqliteHeader(string path)
    {
        Span<byte> header = stackalloc byte[16];
        using var stream = File.OpenRead(path);
        var bytesRead = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);

        if (bytesRead < 16 || !header.SequenceEqual(SqliteHeaderMagic))
        {
            throw new InvalidOperationException("Die Datei ist keine gültige SQLite-Datenbankdatei.");
        }
    }

    private static BackupFileInfo ToBackupFileInfo(string path)
    {
        var fileInfo = new FileInfo(path);
        return new BackupFileInfo
        {
            FileName = fileInfo.Name,
            IsAutomatic = fileInfo.Name.StartsWith(AutoPrefix + "_", StringComparison.OrdinalIgnoreCase),
            CreatedAtUtc = fileInfo.LastWriteTimeUtc,
            SizeBytes = fileInfo.Length,
        };
    }
}
