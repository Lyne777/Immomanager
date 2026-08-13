using System.Text;
using UglyToad.PdfPig;

namespace Immomanager.Web.Services;

/// <summary>Extrahiert reinen Text aus PDF-Exposés via PdfPig. Liefert nur eingebetteten Text - bei
/// rein gescannten (bildbasierten) PDFs ohne Text-Layer bleibt das Ergebnis leer.</summary>
public class ExposeParserService : IExposeParserService
{
    private readonly ILogger<ExposeParserService> _logger;

    public ExposeParserService(ILogger<ExposeParserService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExtractTextAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        await pdfStream.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        try
        {
            return await Task.Run(() =>
            {
                var text = new StringBuilder();
                using var document = PdfDocument.Open(memory);
                foreach (var page in document.GetPages())
                {
                    text.AppendLine(page.Text);
                }

                return text.ToString();
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // PdfPig wirft z. B. PdfDocumentFormatException/PdfDocumentEncryptedException bei
            // beschädigten, passwortgeschützten oder gar keinen echten PDF-Dateien - hier bewusst
            // breit gefangen und in eine für die UI konsistent behandelbare Fehlermeldung übersetzt,
            // statt den Blazor-Circuit mit einer unbehandelten Exception abstürzen zu lassen.
            _logger.LogError(ex, "PDF-Textextraktion fehlgeschlagen.");
            throw new InvalidOperationException(
                "Die Datei konnte nicht als PDF gelesen werden - ist sie beschädigt oder passwortgeschützt?", ex);
        }
    }
}
