using System.Globalization;
using Immomanager.Web.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Immomanager.Web.Services;

/// <summary>Erstellt ein Mieterschreiben (Mahnung/Anschreiben/Kündigung) als PDF-Entwurf via QuestPDF:
/// den eigentlichen Brieftext formuliert Claude im Rahmen des Tool-Aufrufs (er kennt den Grund aus
/// dem Gesprächsverlauf), dieser Service übernimmt nur den korrekten Absender-/Empfänger-/
/// Objektbezug und die Formatierung. Bewusst NUR ein Entwurf zum Download - kein automatischer
/// Versand (weder postalisch noch per E-Mail), das bleibt immer eine bewusste Aktion des Nutzers.</summary>
public class TenantLetterPdfGenerator : ITenantLetterPdfGenerator
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    private static readonly Dictionary<TenantLetterType, string> TypeLabels = new()
    {
        [TenantLetterType.Mahnung] = "Mahnung",
        [TenantLetterType.Anschreiben] = "Anschreiben",
        [TenantLetterType.Kuendigung] = "Kündigung",
    };

    private readonly IPropertyService _propertyService;
    private readonly StorageOptions _storageOptions;

    public TenantLetterPdfGenerator(IPropertyService propertyService, StorageOptions storageOptions)
    {
        _propertyService = propertyService;
        _storageOptions = storageOptions;
    }

    public async Task<(string FileName, string Url)> GenerateAsync(
        int propertyId,
        int unitId,
        TenantLetterType letterType,
        string subject,
        string bodyText,
        string? senderName,
        string? senderAddress,
        CancellationToken cancellationToken = default)
    {
        var property = await _propertyService.GetByIdAsync(propertyId)
            ?? throw new InvalidOperationException($"Immobilie mit Id {propertyId} wurde nicht gefunden.");

        var unit = property.Units.FirstOrDefault(u => u.Id == unitId)
            ?? throw new InvalidOperationException($"Einheit mit Id {unitId} wurde bei \"{property.Name}\" nicht gefunden.");

        var tenancy = unit.CurrentTenancy
            ?? throw new InvalidOperationException(
                $"Für \"{unit.Label}\" bei \"{property.Name}\" ist kein aktuelles Mietverhältnis hinterlegt - bitte zuerst anlegen.");

        var effectiveSenderName = string.IsNullOrWhiteSpace(senderName) ? "Vermieter" : senderName;

        var fileName = $"{TypeLabels[letterType]}_{SanitizeFileName(tenancy.TenantName)}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
        var absoluteOutputPath = Path.Combine(_storageOptions.ExportsDirectoryAbsolute, fileName);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Calibri));

                page.Content().Column(col =>
                {
                    col.Spacing(4);

                    col.Item().Text(effectiveSenderName).FontSize(9).FontColor(Colors.Grey.Darken2);
                    if (!string.IsNullOrWhiteSpace(senderAddress))
                    {
                        col.Item().Text(senderAddress).FontSize(9).FontColor(Colors.Grey.Darken2);
                    }

                    col.Item().PaddingTop(20).Text(tenancy.TenantName);
                    col.Item().Text($"{property.Address}");
                    col.Item().Text($"{unit.Label}");

                    col.Item().PaddingTop(20).AlignRight().Text(DateTime.Now.ToString("d", De));

                    col.Item().PaddingTop(20).Text(subject).Bold().FontSize(13);

                    col.Item().PaddingTop(10).Text(bodyText).LineHeight(1.4f);

                    col.Item().PaddingTop(20).Text("Mit freundlichen Grüßen");
                    col.Item().PaddingTop(30).Text(effectiveSenderName);
                });

                page.Footer().PaddingTop(10).Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                    col.Item().PaddingTop(4).Text(
                        "Entwurf, erstellt von Armin Asset (KI) - keine Rechtsberatung. Bitte vor Versand inhaltlich " +
                        "und (insbesondere bei Kündigungen) rechtlich prüfen."
                    ).FontSize(7).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf(absoluteOutputPath);

        var relativeUrl = $"/data-files/{StorageOptions.ExportsRelativeRoot}/{fileName}";
        return (fileName, relativeUrl);
    }

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray()).Replace(' ', '_');
        return string.IsNullOrWhiteSpace(sanitized) ? "Mieter" : sanitized;
    }
}
