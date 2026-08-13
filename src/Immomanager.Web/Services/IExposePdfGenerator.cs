namespace Immomanager.Web.Services;

public interface IExposePdfGenerator
{
    /// <summary>Erstellt ein PDF-Exposé für die angegebene Immobilie und speichert es unter
    /// {DataDirectory}/exports/. Liefert Dateiname und relative URL (unter /data-files/...) zurück.</summary>
    Task<(string FileName, string RelativeUrl)> GenerateAsync(int propertyId, CancellationToken cancellationToken = default);
}
