using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

/// <summary>Erstellt einen Mieterschreiben-Entwurf (Mahnung/Anschreiben/Kündigung) als Word-Dokument
/// (.docx) via DocumentFormat.OpenXml: den eigentlichen Brieftext formuliert Claude im Rahmen des
/// Tool-Aufrufs, dieser Service übernimmt nur den korrekten Absender-/Empfänger-/Objektbezug und die
/// Formatierung. Bewusst .docx statt PDF, damit der Nutzer die von Armin formulierten Texte danach noch
/// bequem in Word anpassen kann. Ist beim Objekt ein Eigentümer mit hinterlegtem Briefkopf gepflegt,
/// wird dessen .docx als Vorlage kopiert und der Brieftext an das bestehende Dokument angehängt (dessen
/// Kopf-/Fußzeile mit Logo/Kontaktdaten bleibt dadurch automatisch erhalten) - sonst wird ein einfaches
/// neues Dokument mit Absenderzeile erzeugt. Bewusst NUR ein Entwurf zum Download - kein automatischer
/// Versand (weder postalisch noch per E-Mail), das bleibt immer eine bewusste Aktion des Nutzers.</summary>
public class TenantLetterWordGenerator : ITenantLetterGenerator
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

    public TenantLetterWordGenerator(IPropertyService propertyService, StorageOptions storageOptions)
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
        CancellationToken cancellationToken = default)
    {
        var property = await _propertyService.GetByIdAsync(propertyId)
            ?? throw new InvalidOperationException($"Immobilie mit Id {propertyId} wurde nicht gefunden.");

        var unit = property.Units.FirstOrDefault(u => u.Id == unitId)
            ?? throw new InvalidOperationException($"Einheit mit Id {unitId} wurde bei \"{property.Name}\" nicht gefunden.");

        var tenancy = unit.CurrentTenancy
            ?? throw new InvalidOperationException(
                $"Für \"{unit.Label}\" bei \"{property.Name}\" ist kein aktuelles Mietverhältnis hinterlegt - bitte zuerst anlegen.");

        var owner = property.Owner;
        var effectiveSenderName = string.IsNullOrWhiteSpace(owner?.Name) ? "Vermieter" : owner!.Name;

        var fileName = $"{TypeLabels[letterType]}_{SanitizeFileName(tenancy.TenantName)}_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
        var absoluteOutputPath = Path.Combine(_storageOptions.ExportsDirectoryAbsolute, fileName);

        var letterheadAbsolutePath = owner?.LetterheadFilePath is { } relativePath
            ? Path.Combine(_storageOptions.DataDirectoryAbsolute, relativePath)
            : null;

        if (letterheadAbsolutePath is not null && File.Exists(letterheadAbsolutePath))
        {
            File.Copy(letterheadAbsolutePath, absoluteOutputPath, overwrite: true);

            using var document = WordprocessingDocument.Open(absoluteOutputPath, isEditable: true);
            var body = document.MainDocumentPart!.Document!.Body!;
            AppendLetterContent(body, property, unit, tenancy, subject, bodyText, effectiveSenderName, owner, includeSenderBlock: false);
            document.MainDocumentPart.Document!.Save();
        }
        else
        {
            using var document = WordprocessingDocument.Create(absoluteOutputPath, WordprocessingDocumentType.Document);
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(BuildSectionProperties()));

            var body = mainPart.Document.Body!;
            AppendLetterContent(body, property, unit, tenancy, subject, bodyText, effectiveSenderName, owner, includeSenderBlock: true);
            mainPart.Document.Save();
        }

        var relativeUrl = $"/data-files/{StorageOptions.ExportsRelativeRoot}/{fileName}";
        return (fileName, relativeUrl);
    }

    private static void AppendLetterContent(
        Body body,
        Property property,
        PropertyUnit unit,
        Tenancy tenancy,
        string subject,
        string bodyText,
        string senderName,
        Owner? owner,
        bool includeSenderBlock)
    {
        // Neue Absätze werden vor die vorhandene SectionProperties eingefügt (falls z. B. aus dem
        // Briefkopf-Template übernommen), statt sie einfach ans Ende zu hängen - w:sectPr muss laut
        // OOXML-Schema immer das letzte Element im Body bleiben.
        var sectPr = body.Elements<SectionProperties>().FirstOrDefault();
        void InsertPara(Paragraph paragraph)
        {
            if (sectPr is not null)
            {
                body.InsertBefore(paragraph, sectPr);
            }
            else
            {
                body.AppendChild(paragraph);
            }
        }

        if (includeSenderBlock)
        {
            InsertPara(CreateParagraph(senderName, fontSizeHalfPoints: 18, color: "595959"));
            if (!string.IsNullOrWhiteSpace(owner?.Address))
            {
                foreach (var line in SplitLines(owner!.Address!))
                {
                    InsertPara(CreateParagraph(line, fontSizeHalfPoints: 18, color: "595959"));
                }
            }
            InsertPara(EmptyParagraph());
        }

        InsertPara(CreateParagraph(tenancy.TenantName));
        InsertPara(CreateParagraph(property.Address));
        InsertPara(CreateParagraph(unit.Label));
        InsertPara(EmptyParagraph());

        InsertPara(CreateParagraph(DateTime.Now.ToString("d", De), alignment: JustificationValues.Right));
        InsertPara(EmptyParagraph());

        InsertPara(CreateParagraph(subject, bold: true, fontSizeHalfPoints: 26));
        InsertPara(EmptyParagraph());

        foreach (var line in SplitLines(bodyText))
        {
            InsertPara(string.IsNullOrWhiteSpace(line) ? EmptyParagraph() : CreateParagraph(line));
        }

        InsertPara(EmptyParagraph());
        InsertPara(CreateParagraph("Mit freundlichen Grüßen"));
        InsertPara(EmptyParagraph());
        InsertPara(EmptyParagraph());
        InsertPara(CreateParagraph(senderName));

        InsertPara(EmptyParagraph());
        InsertPara(CreateParagraph(
            "Entwurf, erstellt von Armin Asset (KI) - keine Rechtsberatung. Bitte vor Versand inhaltlich " +
            "und (insbesondere bei Kündigungen) rechtlich prüfen.",
            italic: true, fontSizeHalfPoints: 14, color: "808080"));
    }

    private static SectionProperties BuildSectionProperties() =>
        new(new PageMargin { Top = 1417, Bottom = 1417, Left = 1417, Right = 1417, Header = 708, Footer = 708, Gutter = 0 });

    private static IEnumerable<string> SplitLines(string text) =>
        text.Replace("\r\n", "\n").Split('\n');

    private static Paragraph EmptyParagraph() => new();

    private static Paragraph CreateParagraph(
        string text,
        bool bold = false,
        bool italic = false,
        int? fontSizeHalfPoints = null,
        string? color = null,
        JustificationValues? alignment = null)
    {
        var runProperties = new RunProperties();
        if (bold) runProperties.Append(new Bold());
        if (italic) runProperties.Append(new Italic());
        if (fontSizeHalfPoints.HasValue) runProperties.Append(new FontSize { Val = fontSizeHalfPoints.Value.ToString() });
        if (color is not null) runProperties.Append(new Color { Val = color });

        var run = new Run(runProperties, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        var paragraph = new Paragraph(run);
        if (alignment.HasValue)
        {
            paragraph.ParagraphProperties = new ParagraphProperties(new Justification { Val = alignment.Value });
        }

        return paragraph;
    }

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray()).Replace(' ', '_');
        return string.IsNullOrWhiteSpace(sanitized) ? "Mieter" : sanitized;
    }
}
