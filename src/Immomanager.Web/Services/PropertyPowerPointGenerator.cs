using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Immomanager.Web.Models;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace Immomanager.Web.Services;

/// <summary>Erstellt eine rudimentäre .pptx-Präsentation (Titelfolie, Objektdaten/KPIs, Finanzierung)
/// für Banktermine via DocumentFormat.OpenXml (offizielles, MIT-lizenziertes Microsoft-SDK) und
/// speichert sie unter {DataDirectory}/exports/.</summary>
public class PropertyPowerPointGenerator : IPropertyPowerPointGenerator
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    private const long SlideWidthEmu = 12192000; // 13.33in, 16:9
    private const long SlideHeightEmu = 6858000; // 7.5in

    private readonly IPropertyService _propertyService;
    private readonly KpiCalculationService _kpiService;
    private readonly StorageOptions _storageOptions;

    public PropertyPowerPointGenerator(IPropertyService propertyService, KpiCalculationService kpiService, StorageOptions storageOptions)
    {
        _propertyService = propertyService;
        _kpiService = kpiService;
        _storageOptions = storageOptions;
    }

    public async Task<(string FileName, string RelativeUrl)> GenerateAsync(int propertyId, CancellationToken cancellationToken = default)
    {
        var property = await _propertyService.GetByIdAsync(propertyId)
            ?? throw new InvalidOperationException($"Immobilie mit Id {propertyId} wurde nicht gefunden.");

        var kpi = _kpiService.Calculate(property, ViewMode.TotalObject);

        var fileName = $"Praesentation_{SanitizeFileName(property.Name)}_{DateTime.Now:yyyyMMdd_HHmmss}.pptx";
        var absoluteOutputPath = Path.Combine(_storageOptions.ExportsDirectoryAbsolute, fileName);

        using (var document = PresentationDocument.Create(absoluteOutputPath, PresentationDocumentType.Presentation))
        {
            var presentationPart = document.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation();

            var slideMasterPart = CreateSlideMasterPart(presentationPart);

            var slideParts = new List<SlidePart>
            {
                CreateSlide(presentationPart, slideMasterPart, property.Name, new[] { property.Address }),
                CreateSlide(presentationPart, slideMasterPart, "Objektdaten & Kennzahlen", new[]
                {
                    $"Baujahr: {property.YearBuilt?.ToString() ?? "-"}",
                    $"Wohn-/Nutzfläche: {property.LivingAreaSqm:N2} m²",
                    $"Kaufpreis: {FormatCurrency(property.PurchasePrice)}",
                    $"Geschätzter Marktwert: {FormatCurrency(property.CurrentMarketValue)}",
                    $"Bruttomietrendite: {FormatPercent(kpi.GrossRentalYieldPercent)}",
                    $"Nettomietrendite: {FormatPercent(kpi.NetRentalYieldPercent)}",
                    $"Cashflow vor Steuern / Monat: {FormatCurrency(kpi.CashflowMonthly)}",
                }),
                CreateSlide(presentationPart, slideMasterPart, "Finanzierung", property.Financings.Count == 0
                    ? new[] { "Keine Fremdfinanzierung erfasst." }
                    : property.Financings.Select(f =>
                        $"{f.BankName}: Restschuld {FormatCurrency(f.CurrentRemainingDebt)}, " +
                        $"Zins {f.InterestRatePercent:N2} %, Rate {FormatCurrency(f.MonthlyPayment)}/Monat").ToArray()),
            };

            BuildPresentationStructure(presentationPart, slideMasterPart, slideParts);
            presentationPart.Presentation.Save();
        }

        var relativeUrl = $"/data-files/{StorageOptions.ExportsRelativeRoot}/{fileName}";
        return (fileName, relativeUrl);
    }

    private static void BuildPresentationStructure(PresentationPart presentationPart, SlideMasterPart slideMasterPart, List<SlidePart> slideParts)
    {
        var presentation = presentationPart.Presentation;

        presentation.SlideMasterIdList = new P.SlideMasterIdList(
            new P.SlideMasterId { Id = 2147483648U, RelationshipId = presentationPart.GetIdOfPart(slideMasterPart) });

        var slideIdList = new P.SlideIdList();
        uint slideId = 256;
        foreach (var slidePart in slideParts)
        {
            slideIdList.Append(new P.SlideId { Id = slideId++, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
        }

        presentation.SlideIdList = slideIdList;
        presentation.SlideSize = new P.SlideSize { Cx = (int)SlideWidthEmu, Cy = (int)SlideHeightEmu };
        presentation.NotesSize = new P.NotesSize { Cx = 6858000, Cy = 9144000 };
    }

    private static SlideMasterPart CreateSlideMasterPart(PresentationPart presentationPart)
    {
        var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>();
        var themePart = slideMasterPart.AddNewPart<ThemePart>();
        themePart.Theme = CreateTheme();

        var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>();
        slideLayoutPart.SlideLayout = CreateBlankLayout();

        slideMasterPart.SlideMaster = new P.SlideMaster(
            new P.CommonSlideData(
                new P.ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new P.ApplicationNonVisualDrawingProperties()),
                    new P.GroupShapeProperties(new D.TransformGroup()))),
            new P.ColorMap
            {
                Background1 = D.ColorSchemeIndexValues.Light1,
                Text1 = D.ColorSchemeIndexValues.Dark1,
                Background2 = D.ColorSchemeIndexValues.Light2,
                Text2 = D.ColorSchemeIndexValues.Dark2,
                Accent1 = D.ColorSchemeIndexValues.Accent1,
                Accent2 = D.ColorSchemeIndexValues.Accent2,
                Accent3 = D.ColorSchemeIndexValues.Accent3,
                Accent4 = D.ColorSchemeIndexValues.Accent4,
                Accent5 = D.ColorSchemeIndexValues.Accent5,
                Accent6 = D.ColorSchemeIndexValues.Accent6,
                Hyperlink = D.ColorSchemeIndexValues.Hyperlink,
                FollowedHyperlink = D.ColorSchemeIndexValues.FollowedHyperlink,
            },
            new P.SlideLayoutIdList(new P.SlideLayoutId { Id = 2147483649U, RelationshipId = slideMasterPart.GetIdOfPart(slideLayoutPart) }));

        return slideMasterPart;
    }

    private static P.SlideLayout CreateBlankLayout() =>
        new(
            new P.CommonSlideData(
                new P.ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new P.ApplicationNonVisualDrawingProperties()),
                    new P.GroupShapeProperties(new D.TransformGroup()))) { Name = "Leer" },
            new P.ColorMapOverride(new D.MasterColorMapping()))
        {
            Type = P.SlideLayoutValues.Blank,
        };

    private static SlidePart CreateSlide(PresentationPart presentationPart, SlideMasterPart slideMasterPart, string title, IReadOnlyList<string> bodyLines)
    {
        var slideLayoutPart = slideMasterPart.SlideLayoutParts.First();
        var slidePart = presentationPart.AddNewPart<SlidePart>();
        slidePart.AddPart(slideLayoutPart);

        var shapeTree = new P.ShapeTree(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new D.TransformGroup()),
            CreateTextBoxShape(2, "Titel", title, isTitle: true, x: 457200, y: 274638, width: SlideWidthEmu - 914400, height: 914400),
            CreateTextBoxShape(3, "Inhalt", string.Join('\n', bodyLines.Select(l => "• " + l)), isTitle: false, x: 457200, y: 1470025, width: SlideWidthEmu - 914400, height: SlideHeightEmu - 1900000));

        slidePart.Slide = new P.Slide(new P.CommonSlideData(shapeTree));
        return slidePart;
    }

    private static P.Shape CreateTextBoxShape(uint id, string name, string text, bool isTitle, long x, long y, long width, long height)
    {
        var shape = new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = name },
                new P.NonVisualShapeDrawingProperties(new D.ShapeLocks { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(
                new D.Transform2D(
                    new D.Offset { X = x, Y = y },
                    new D.Extents { Cx = width, Cy = height }),
                new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.Rectangle }),
            new P.TextBody(
                new D.BodyProperties(),
                new D.ListStyle()));

        foreach (var line in text.Split('\n'))
        {
            shape.TextBody!.Append(new D.Paragraph(
                new D.Run(
                    new D.RunProperties { Language = "de-DE", FontSize = isTitle ? 3200 : 1800, Bold = isTitle },
                    new D.Text(line))));
        }

        return shape;
    }

    private static D.Theme CreateTheme()
    {
        var colorScheme = new D.ColorScheme(
            new D.Dark1Color(new D.SystemColor { Val = D.SystemColorValues.WindowText, LastColor = "000000" }),
            new D.Light1Color(new D.SystemColor { Val = D.SystemColorValues.Window, LastColor = "FFFFFF" }),
            new D.Dark2Color(new D.RgbColorModelHex { Val = "1F497D" }),
            new D.Light2Color(new D.RgbColorModelHex { Val = "EEECE1" }),
            new D.Accent1Color(new D.RgbColorModelHex { Val = "1565C0" }),
            new D.Accent2Color(new D.RgbColorModelHex { Val = "2E7D32" }),
            new D.Accent3Color(new D.RgbColorModelHex { Val = "9BBB59" }),
            new D.Accent4Color(new D.RgbColorModelHex { Val = "8064A2" }),
            new D.Accent5Color(new D.RgbColorModelHex { Val = "4BACC6" }),
            new D.Accent6Color(new D.RgbColorModelHex { Val = "F79646" }),
            new D.Hyperlink(new D.RgbColorModelHex { Val = "0000FF" }),
            new D.FollowedHyperlinkColor(new D.RgbColorModelHex { Val = "800080" })) { Name = "Immomanager" };

        var fontScheme = new D.FontScheme(
            new D.MajorFont(new D.LatinFont { Typeface = "Calibri" }, new D.EastAsianFont { Typeface = "" }, new D.ComplexScriptFont { Typeface = "" }),
            new D.MinorFont(new D.LatinFont { Typeface = "Calibri" }, new D.EastAsianFont { Typeface = "" }, new D.ComplexScriptFont { Typeface = "" }))
        { Name = "Immomanager" };

        var formatScheme = new D.FormatScheme(
            new D.FillStyleList(
                new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor }),
                new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor }),
                new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor })),
            new D.LineStyleList(
                new D.Outline(new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor })),
                new D.Outline(new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor })),
                new D.Outline(new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor }))),
            new D.EffectStyleList(
                new D.EffectStyle(new D.EffectList()),
                new D.EffectStyle(new D.EffectList()),
                new D.EffectStyle(new D.EffectList())),
            new D.BackgroundFillStyleList(
                new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor }),
                new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor }),
                new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.PhColor })))
        { Name = "Immomanager" };

        return new D.Theme(new D.ThemeElements(colorScheme, fontScheme, formatScheme)) { Name = "Immomanager" };
    }

    private static string FormatCurrency(decimal value) => value.ToString("C2", De);
    private static string FormatPercent(decimal value) => value.ToString("N2", De) + " %";

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray()).Replace(' ', '_');
        return string.IsNullOrWhiteSpace(sanitized) ? "Immobilie" : sanitized;
    }
}
