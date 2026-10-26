namespace Immomanager.Web.Services;

public record ClaudeExportResult(bool Success, string Message);

public interface IClaudeExportService
{
    /// <summary>Schreibt den kompletten Export in den konfigurierten Ordner (siehe
    /// <see cref="StorageOptions.ClaudeExportDirectoryAbsolute"/>). Läuft bereits ein Export, kehrt der
    /// Aufruf sofort mit einer entsprechenden Meldung zurück, statt einen zweiten parallel zu starten.</summary>
    Task<ClaudeExportResult> ExportAsync(CancellationToken cancellationToken = default);
}
