using System.Globalization;
using Immomanager.Web.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Immomanager.Web.Services;

/// <summary>Erstellt eine nach Kategorie gruppierte Einkaufslisten-PDF (Kästchen zum Abhaken beim
/// Einkaufen) via QuestPDF und speichert sie unter {DataDirectory}/exports/.</summary>
public class ShoppingListPdfGenerator : IShoppingListPdfGenerator
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    private readonly IShoppingListService _shoppingListService;
    private readonly StorageOptions _storageOptions;

    public ShoppingListPdfGenerator(IShoppingListService shoppingListService, StorageOptions storageOptions)
    {
        _shoppingListService = shoppingListService;
        _storageOptions = storageOptions;
    }

    public async Task<(string FileName, string Url)> GenerateAsync(int shoppingListId, CancellationToken cancellationToken = default)
    {
        var list = await _shoppingListService.GetByIdAsync(shoppingListId)
            ?? throw new InvalidOperationException($"Einkaufsliste mit Id {shoppingListId} wurde nicht gefunden.");

        var fileName = $"Einkaufsliste_{list.CreatedAt:yyyyMMdd_HHmmss}.pdf";
        var absoluteOutputPath = Path.Combine(_storageOptions.ExportsDirectoryAbsolute, fileName);

        var groups = list.Items
            .OrderBy(i => i.Category)
            .ThenBy(i => i.Name)
            .GroupBy(i => i.Category)
            .ToList();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Calibri));

                page.Content().Column(col =>
                {
                    col.Spacing(4);

                    col.Item().Text("Einkaufsliste").FontSize(20).Bold();
                    col.Item().PaddingBottom(10).Text(list.CreatedAt.ToLocalTime().ToString("d. MMMM yyyy", De))
                        .FontSize(10).FontColor(Colors.Grey.Darken1);

                    foreach (var group in groups)
                    {
                        col.Item().PaddingTop(12).Text(group.Key).FontSize(13).Bold();

                        foreach (var item in group)
                        {
                            col.Item().PaddingTop(3).Row(row =>
                            {
                                row.ConstantItem(16).Height(16).Border(1).BorderColor(Colors.Grey.Darken1);
                                row.RelativeItem().PaddingLeft(8).Text(text =>
                                {
                                    var amount = FormatAmount(item.Quantity, item.Unit);
                                    if (!string.IsNullOrEmpty(amount))
                                    {
                                        text.Span(amount + " ").SemiBold();
                                    }
                                    text.Span(item.Name);
                                });
                            });
                        }
                    }
                });

                page.Footer().PaddingTop(10).Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                    col.Item().PaddingTop(4).Text(
                        "Erstellt von Armin Asset (KI) auf Basis des gespeicherten Speiseplans."
                    ).FontSize(7).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf(absoluteOutputPath);

        var relativeUrl = $"/data-files/{StorageOptions.ExportsRelativeRoot}/{fileName}";
        return (fileName, relativeUrl);
    }

    private static string FormatAmount(decimal? quantity, string? unit)
    {
        if (!quantity.HasValue)
        {
            return unit ?? string.Empty;
        }

        var quantityText = quantity.Value.ToString("0.##", De);
        return string.IsNullOrWhiteSpace(unit) ? quantityText : $"{quantityText} {unit}";
    }
}
