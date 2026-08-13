using System.Globalization;
using Immomanager.Web.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Immomanager.Web.Services;

/// <summary>Erstellt ein professionelles PDF-Exposé (Fotos, Objektdaten, Beschreibung, KPI-Übersicht)
/// für eine Immobilie via QuestPDF und speichert es unter {DataDirectory}/exports/.</summary>
public class ExposePdfGenerator : IExposePdfGenerator
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    private readonly IPropertyService _propertyService;
    private readonly IPropertyImageService _imageService;
    private readonly KpiCalculationService _kpiService;
    private readonly StorageOptions _storageOptions;

    public ExposePdfGenerator(
        IPropertyService propertyService,
        IPropertyImageService imageService,
        KpiCalculationService kpiService,
        StorageOptions storageOptions)
    {
        _propertyService = propertyService;
        _imageService = imageService;
        _kpiService = kpiService;
        _storageOptions = storageOptions;
    }

    public async Task<(string FileName, string RelativeUrl)> GenerateAsync(int propertyId, CancellationToken cancellationToken = default)
    {
        var property = await _propertyService.GetByIdAsync(propertyId)
            ?? throw new InvalidOperationException($"Immobilie mit Id {propertyId} wurde nicht gefunden.");

        var images = await _imageService.GetByPropertyIdAsync(propertyId);
        var kpi = _kpiService.Calculate(property, ViewMode.TotalObject);

        var photoBytes = new List<byte[]>();
        foreach (var image in images.Take(4))
        {
            var absolutePath = Path.Combine(_storageOptions.DataDirectoryAbsolute, image.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(absolutePath))
            {
                photoBytes.Add(await File.ReadAllBytesAsync(absolutePath, cancellationToken));
            }
        }

        var fileName = $"Expose_{SanitizeFileName(property.Name)}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
        var absoluteOutputPath = Path.Combine(_storageOptions.ExportsDirectoryAbsolute, fileName);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Calibri));

                page.Header().Column(col =>
                {
                    col.Item().Text(property.Name).FontSize(22).Bold();
                    col.Item().Text(property.Address).FontSize(12).FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Spacing(12);

                    if (photoBytes.Count > 0)
                    {
                        col.Item().Row(row =>
                        {
                            foreach (var bytes in photoBytes)
                            {
                                row.RelativeItem().Height(120).Image(bytes).FitArea();
                            }
                        });
                    }

                    col.Item().Text("Objektdaten").FontSize(14).Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn();
                            c.RelativeColumn();
                        });

                        AddRow(table, "Baujahr", property.YearBuilt?.ToString() ?? "-");
                        AddRow(table, "Wohn-/Nutzfläche", $"{property.LivingAreaSqm:N2} m²");
                        AddRow(table, "Kaufpreis", FormatCurrency(property.PurchasePrice));
                        AddRow(table, "Geschätzter Marktwert", FormatCurrency(property.CurrentMarketValue));
                        AddRow(table, "Kaltmiete / Monat", FormatCurrency(property.CurrentColdRentMonthly));
                    });

                    col.Item().Text("Kennzahlen-Übersicht").FontSize(14).Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn();
                            c.RelativeColumn();
                        });

                        AddRow(table, "Bruttomietrendite", FormatPercent(kpi.GrossRentalYieldPercent));
                        AddRow(table, "Nettomietrendite", FormatPercent(kpi.NetRentalYieldPercent));
                        AddRow(table, "Cashflow vor Steuern / Monat", FormatCurrency(kpi.CashflowMonthly));
                        AddRow(table, "Beleihungsauslauf (LTV)", FormatPercent(kpi.LoanToValuePercent));
                    });

                    if (!string.IsNullOrWhiteSpace(property.Notes))
                    {
                        col.Item().Text("Beschreibung").FontSize(14).Bold();
                        col.Item().Text(property.Notes);
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Erstellt von Armin Asset am ").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.Span(DateTime.Now.ToString("d", De)).FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf(absoluteOutputPath);

        var relativeUrl = $"/data-files/{StorageOptions.ExportsRelativeRoot}/{fileName}";
        return (fileName, relativeUrl);
    }

    private static void AddRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Padding(2).Text(label).SemiBold();
        table.Cell().Padding(2).Text(value);
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
