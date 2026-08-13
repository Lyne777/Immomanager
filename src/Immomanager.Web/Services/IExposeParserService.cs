namespace Immomanager.Web.Services;

public interface IExposeParserService
{
    /// <summary>Liest den Rohtext aus allen Seiten eines PDF-Exposés.</summary>
    Task<string> ExtractTextAsync(Stream pdfStream, CancellationToken cancellationToken = default);
}
