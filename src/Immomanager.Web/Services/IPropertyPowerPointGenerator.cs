namespace Immomanager.Web.Services;

public interface IPropertyPowerPointGenerator
{
    /// <summary>Erstellt eine rudimentäre .pptx-Präsentation (Titel, Objektdaten/KPIs, Finanzierung)
    /// für Banktermine und speichert sie unter {DataDirectory}/exports/.</summary>
    Task<(string FileName, string RelativeUrl)> GenerateAsync(int propertyId, CancellationToken cancellationToken = default);
}
