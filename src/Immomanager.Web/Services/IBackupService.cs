using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public interface IBackupService
{
    Task<List<BackupFileInfo>> GetBackupsAsync();

    Task<BackupFileInfo> CreateManualBackupAsync(CancellationToken cancellationToken = default);

    Task DeleteBackupAsync(string fileName);

    Task RestoreFromExistingAsync(string fileName, CancellationToken cancellationToken = default);

    Task RestoreFromUploadAsync(Stream uploadedStream, CancellationToken cancellationToken = default);

    Task<BackupSettings> GetSettingsAsync();

    Task SaveSettingsAsync(BackupSettings settings);

    /// <summary>Führt beim Programmstart ein automatisches Backup aus, sofern in den Einstellungen
    /// aktiviert, und bereinigt anschließend alte automatische Sicherungen gemäß Aufbewahrungsanzahl.</summary>
    Task RunStartupAutoBackupIfEnabledAsync(CancellationToken cancellationToken = default);
}
