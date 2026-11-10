using System.Globalization;
using System.Text;
using Immomanager.Web.Data;
using Immomanager.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Immomanager.Web.Services;

/// <summary>Schreibt die Objektdaten als lesbare Dateien (Markdown, CSV, DB-Snapshot, optional
/// Dokument-Spiegel) in einen Ordner, den Synology Cloud Sync nach OneDrive/SharePoint abgleichen kann -
/// damit Claude (Desktop, Web, Handy, Projekte) die Daten über den Microsoft-365-Connector lesen kann,
/// ohne dass die NAS aus dem Internet erreichbar sein muss. Bewusst ein reiner Lese-Export: die
/// Live-Datenbank (SQLite/WAL) liegt nie in diesem Ordner, Änderungen passieren nur in Immomanager.
/// Konzept siehe CLAUDE.md im Repository.</summary>
public class ClaudeExportService : IClaudeExportService
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly UTF8Encoding Utf8WithBom = new(true);

    private readonly IPropertyService _propertyService;
    private readonly IPropertyLogService _logService;
    private readonly KpiCalculationService _kpiService;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly StorageOptions _storage;
    private readonly ClaudeExportOptions _options;
    private readonly ClaudeExportState _state;
    private readonly ILogger<ClaudeExportService> _logger;

    public ClaudeExportService(
        IPropertyService propertyService,
        IPropertyLogService logService,
        KpiCalculationService kpiService,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        StorageOptions storage,
        IOptions<ClaudeExportOptions> options,
        ClaudeExportState state,
        ILogger<ClaudeExportService> logger)
    {
        _propertyService = propertyService;
        _logService = logService;
        _kpiService = kpiService;
        _contextFactory = contextFactory;
        _storage = storage;
        _options = options.Value;
        _state = state;
        _logger = logger;
    }

    public async Task<ClaudeExportResult> ExportAsync(CancellationToken cancellationToken = default)
    {
        if (!await _state.RunLock.WaitAsync(0, cancellationToken))
        {
            return new ClaudeExportResult(false, "Ein Export läuft bereits.");
        }

        try
        {
            var (objectCount, documentCount) = await RunAsync(cancellationToken);
            _state.RecordSuccess(objectCount, documentCount, _options.MirrorDocuments);
            _logger.LogInformation("Export für Claude abgeschlossen: {Objects} Objekte, {Documents} Dokumente (Kopien: {Mirrored}).",
                objectCount, documentCount, _options.MirrorDocuments);
            return new ClaudeExportResult(true, _state.LastMessage!);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export für Claude fehlgeschlagen.");
            _state.RecordFailure($"Export fehlgeschlagen: {ex.Message}");
            return new ClaudeExportResult(false, _state.LastMessage!);
        }
        finally
        {
            _state.RunLock.Release();
        }
    }

    private async Task<(int Objects, int Documents)> RunAsync(CancellationToken ct)
    {
        var root = _storage.ClaudeExportDirectoryAbsolute;
        var objectsDir = Path.Combine(root, "objekte");
        var documentsDir = Path.Combine(root, "dokumente");
        var mirrorDocuments = _options.MirrorDocuments;
        Directory.CreateDirectory(objectsDir);
        if (mirrorDocuments)
        {
            Directory.CreateDirectory(documentsDir);
        }

        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ClaudeExportState.ResolveTimeZone()).DateTime;

        var properties = await _propertyService.GetAllAsync();
        var related = await LoadRelatedAsync(ct);

        var usedObjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var expectedObjectFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var expectedDocumentFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bundles = new List<PropertyBundle>();
        var documentCount = 0;

        foreach (var property in properties)
        {
            ct.ThrowIfCancellationRequested();

            var fileBaseName = UniqueName(SafeName(property.Name, "Objekt"), property.Id, usedObjectNames);
            var mirrors = PlanMirrors(fileBaseName, property, related, usedTargets);

            foreach (var mirror in mirrors)
            {
                var targetPath = Path.Combine(root, mirror.TargetRelativePath.Replace('/', Path.DirectorySeparatorChar));
                mirror.Exists = mirrorDocuments
                    ? MirrorFile(mirror.SourceRelativePath, targetPath)
                    : TryResolveSource(mirror.SourceRelativePath, out _);
                if (mirror.Exists)
                {
                    documentCount++;
                    if (mirrorDocuments)
                    {
                        expectedDocumentFiles.Add(Path.GetFullPath(targetPath));
                    }
                }
            }

            bundles.Add(new PropertyBundle(
                property,
                fileBaseName,
                _kpiService.Calculate(property, ViewMode.TotalObject),
                _kpiService.Calculate(property, ViewMode.MyShare),
                related.Policies.Where(p => p.PropertyId == property.Id).ToList(),
                related.Statements.Where(s => s.PropertyId == property.Id).ToList(),
                related.Renovations.Where(r => r.PropertyId == property.Id).ToList(),
                await _logService.GetCombinedLogAsync(property.Id),
                mirrors));
        }

        foreach (var bundle in bundles)
        {
            var path = Path.Combine(objectsDir, bundle.FileBaseName + ".md");
            await File.WriteAllTextAsync(path, RenderProperty(bundle, nowLocal), Utf8NoBom, ct);
            expectedObjectFiles.Add(Path.GetFullPath(path));
        }

        await File.WriteAllTextAsync(Path.Combine(root, "uebersicht.md"), RenderOverview(bundles, properties, nowLocal), Utf8NoBom, ct);
        await File.WriteAllTextAsync(Path.Combine(root, "objekte.csv"), RenderObjectsCsv(bundles), Utf8WithBom, ct);
        await File.WriteAllTextAsync(Path.Combine(root, "mietverhaeltnisse.csv"), RenderTenanciesCsv(bundles), Utf8WithBom, ct);
        await File.WriteAllTextAsync(Path.Combine(root, "LIESMICH.md"), RenderReadme(bundles.Count, documentCount, mirrorDocuments, nowLocal), Utf8NoBom, ct);

        DeleteOrphans(objectsDir, expectedObjectFiles);
        if (mirrorDocuments)
        {
            DeleteOrphans(documentsDir, expectedDocumentFiles);
        }
        else if (Directory.Exists(documentsDir))
        {
            // Spiegel wurde ausgeschaltet: alte Kopien aufräumen, sonst findet Claude Unterlagen doppelt.
            Directory.Delete(documentsDir, recursive: true);
        }

        await WriteSnapshotAsync(root, ct);

        return (bundles.Count, documentCount);
    }

    private async Task<RelatedData> LoadRelatedAsync(CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        return new RelatedData(
            await db.InsurancePolicies.AsNoTracking().ToListAsync(ct),
            await db.UtilityStatements.AsNoTracking()
                .Include(s => s.Items).Include(s => s.Documents).Include(s => s.PropertyUnit)
                .ToListAsync(ct),
            await db.RenovationProjects.AsNoTracking().Include(r => r.LineItems).ToListAsync(ct),
            await db.PropertyDocuments.AsNoTracking().ToListAsync(ct),
            await db.UnitDocuments.AsNoTracking().ToListAsync(ct));
    }

    // ---- Dokument-Spiegel -------------------------------------------------------------------------

    // Ordnernamen bewusst ASCII (kein "Mietverträge"): Synology Cloud Sync/OneDrive normalisieren
    // Umlaute in Ordnernamen teils unterschiedlich (NFC/NFD) und legen sonst Doppel-Ordner an.
    private List<MirrorEntry> PlanMirrors(string objectFolder, Property property, RelatedData data, HashSet<string> usedTargets)
    {
        var mirrors = new List<MirrorEntry>();

        void Add(string description, string? sourceRelative, string subFolder, string fileName, string? originalName)
        {
            if (string.IsNullOrWhiteSpace(sourceRelative))
            {
                return;
            }

            var target = UniqueTarget($"dokumente/{objectFolder}/{subFolder}/{SafeFileName(fileName)}", usedTargets);
            mirrors.Add(new MirrorEntry(description, sourceRelative, target, originalName));
        }

        foreach (var policy in data.Policies.Where(p => p.PropertyId == property.Id))
        {
            var category = InsuranceService.CategoryDisplayNames[policy.Category];
            Add($"Versicherung {category}", policy.PdfFilePath, "Versicherungen", $"{category}_{policy.PdfFileName ?? "Police.pdf"}", policy.PdfFileName);
        }

        foreach (var unit in property.Units)
        {
            foreach (var tenancy in unit.Tenancies)
            {
                Add($"Mietvertrag {unit.Label} - {tenancy.TenantName}", tenancy.PdfFilePath, "Mietvertraege",
                    $"{unit.Label}_{tenancy.TenantName}_{tenancy.PdfFileName ?? "Mietvertrag.pdf"}", tenancy.PdfFileName);
            }
        }

        foreach (var statement in data.Statements.Where(s => s.PropertyId == property.Id))
        {
            var scope = statement.PropertyUnit?.Label ?? "Gesamtobjekt";
            foreach (var document in statement.Documents)
            {
                Add($"Nebenkostenabrechnung {statement.Year} ({scope})", document.FilePath, "Nebenkosten",
                    $"{statement.Year}_{scope}_{document.FileName}", document.FileName);
            }
        }

        foreach (var document in data.PropertyDocuments.Where(d => d.PropertyId == property.Id))
        {
            var label = DocumentService.PropertyDocumentTypeLabels[document.DocumentType];
            var titleSuffix = string.IsNullOrWhiteSpace(document.Title) ? "" : $" - {document.Title}";
            Add($"{label}{titleSuffix}", document.FilePath, "Objektunterlagen", $"{label}{titleSuffix}_{document.FileName}", document.FileName);
        }

        foreach (var unit in property.Units)
        {
            foreach (var document in data.UnitDocuments.Where(d => d.PropertyUnitId == unit.Id))
            {
                var label = DocumentService.UnitDocumentTypeLabels[document.DocumentType];
                var titleSuffix = string.IsNullOrWhiteSpace(document.Title) ? "" : $" - {document.Title}";
                Add($"{label}{titleSuffix} ({unit.Label})", document.FilePath, $"Einheiten/{SafeName(unit.Label, "Einheit")}",
                    $"{label}{titleSuffix}_{document.FileName}", document.FileName);
            }
        }

        return mirrors;
    }

    private bool TryResolveSource(string sourceRelativePath, out string sourcePath)
    {
        var dataRoot = Path.GetFullPath(_storage.DataDirectoryAbsolute) + Path.DirectorySeparatorChar;
        sourcePath = Path.GetFullPath(Path.Combine(_storage.DataDirectoryAbsolute, sourceRelativePath.Replace('/', Path.DirectorySeparatorChar)));

        // Pfade stammen aus der DB - trotzdem sicherstellen, dass nichts außerhalb des Datenverzeichnisses
        // gelesen/kopiert werden kann.
        return sourcePath.StartsWith(dataRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(sourcePath);
    }

    private bool MirrorFile(string sourceRelativePath, string targetPath)
    {
        if (!TryResolveSource(sourceRelativePath, out var sourcePath))
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var source = new FileInfo(sourcePath);
        var target = new FileInfo(targetPath);
        if (!target.Exists || target.Length != source.Length || source.LastWriteTimeUtc > target.LastWriteTimeUtc)
        {
            File.Copy(sourcePath, targetPath, overwrite: true);
        }

        return true;
    }

    private static void DeleteOrphans(string directory, HashSet<string> expectedFullPaths)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            if (!expectedFullPaths.Contains(Path.GetFullPath(file)))
            {
                File.Delete(file);
            }
        }

        foreach (var subDirectory in Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories)
                     .OrderByDescending(d => d.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(subDirectory).Any())
            {
                Directory.Delete(subDirectory);
            }
        }
    }

    // ---- DB-Snapshot ------------------------------------------------------------------------------

    private async Task WriteSnapshotAsync(string root, CancellationToken ct)
    {
        var temporaryPath = Path.Combine(root, ".immomanager-snapshot.tmp");
        var finalPath = Path.Combine(root, "immomanager-snapshot.db");
        if (File.Exists(temporaryPath))
        {
            File.Delete(temporaryPath);
        }

        // SQLite-eigene Online-Backup-API (wie BackupService) statt Datei-Kopie: konsistent auch bei
        // laufendem Betrieb/WAL. Pooling aus, damit die Zieldatei danach nicht gesperrt bleibt und sich
        // atomar umbenennen lässt (Cloud Sync sieht so nie eine halb geschriebene Datei).
        await using (var source = new SqliteConnection($"Data Source={_storage.DatabaseFilePath};Pooling=False"))
        await using (var destination = new SqliteConnection($"Data Source={temporaryPath};Pooling=False"))
        {
            await source.OpenAsync(ct);
            await destination.OpenAsync(ct);
            source.BackupDatabase(destination);

            // Ohne WAL-Modus im Snapshot entstehen beim Öffnen keine -wal/-shm-Nebendateien im Sync-Ordner.
            await using var command = destination.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=DELETE;";
            await command.ExecuteNonQueryAsync(ct);
        }

        File.Move(temporaryPath, finalPath, overwrite: true);
    }

    // ---- Rendering --------------------------------------------------------------------------------

    private string RenderProperty(PropertyBundle bundle, DateTime nowLocal)
    {
        var p = bundle.Property;
        var sb = new StringBuilder();
        sb.AppendLine($"# {p.Name}");
        sb.AppendLine();
        sb.AppendLine($"_Stand: {nowLocal:dd.MM.yyyy HH:mm} - Export aus Immomanager, nur lesen. Änderungen nur in Immomanager vornehmen._");
        sb.AppendLine();

        sb.AppendLine("## Stammdaten");
        sb.AppendLine();
        sb.AppendLine($"- Adresse: {p.Address}");
        sb.AppendLine($"- Baujahr: {p.YearBuilt?.ToString() ?? "-"}");
        sb.AppendLine($"- Kaufdatum: {D(p.PurchaseDate)}");
        sb.AppendLine($"- Kaufpreis: {Eur(p.PurchasePrice)} (Nebenkosten: Grunderwerbsteuer {Eur(p.PropertyTransferTax)}, Notar/Grundbuch {Eur(p.NotaryAndRegistryCosts)}, Makler {Eur(p.BrokerCommission)}, Modernisierung beim Kauf {Eur(p.InitialRenovationCosts)})");
        sb.AppendLine($"- Gesamtinvestition: {Eur(p.TotalInvestment)}");
        sb.AppendLine($"- Beteiligungsquote: {Pct(p.OwnershipSharePercent)}");
        sb.AppendLine($"- Eigentümer: {(p.Owner is null ? "nicht hinterlegt" : $"{p.Owner.Name}{(string.IsNullOrWhiteSpace(p.Owner.Address) ? "" : $", {p.Owner.Address.ReplaceLineEndings(", ")}")}")}");
        sb.AppendLine($"- Geschätzter Marktwert: {Eur(p.CurrentMarketValue)}");
        sb.AppendLine($"- Wohn-/Nutzfläche gesamt: {p.LivingAreaSqm.ToString("N2", De)} m²");
        sb.AppendLine($"- Kaltmiete gesamt: {Eur(p.CurrentColdRentMonthly)} / Monat");
        sb.AppendLine($"- Nicht umlegbare Kosten gesamt: {Eur(p.NonAllocableCostsMonthly)} / Monat");
        if (p.IncomeMultiplier is { } multiplier)
        {
            sb.AppendLine($"- Ertragswert-Vervielfältiger (vom Nutzer frei gewählter Faktor, keine Marktbeobachtung): {multiplier.ToString("N1", De)}");
        }
        else
        {
            sb.AppendLine("- Ertragswert-Vervielfältiger: nicht hinterlegt (daher wird kein Ertragswert berechnet)");
        }
        if (!string.IsNullOrWhiteSpace(p.Notes))
        {
            sb.AppendLine($"- Notizen: {p.Notes.ReplaceLineEndings(" ")}");
        }
        sb.AppendLine();

        sb.AppendLine("## Kennzahlen");
        sb.AppendLine();
        sb.AppendLine($"| Kennzahl | Gesamtobjekt | Mein Anteil ({Pct(bundle.KpiMine.SharePercent)}) |");
        sb.AppendLine("|---|---|---|");
        void Kpi(string label, Func<PropertyKpi, string> format) =>
            sb.AppendLine($"| {label} | {format(bundle.KpiTotal)} | {format(bundle.KpiMine)} |");
        Kpi("Kaltmiete / Jahr", k => Eur(k.ColdRentAnnual));
        Kpi("Nicht umlegbare Kosten / Jahr", k => Eur(k.NonAllocableCostsAnnual));
        Kpi("Monatlicher Kapitaldienst", k => Eur(k.TotalMonthlyDebtService));
        Kpi("Cashflow vor Steuern / Monat", k => Eur(k.CashflowMonthly));
        Kpi("Restschuld", k => Eur(k.TotalRemainingDebt));
        Kpi("Eingesetztes Eigenkapital", k => Eur(k.EquityInvested));
        Kpi("Aktueller Eigenkapitalwert", k => Eur(k.CurrentEquityValue));
        Kpi("Bruttomietrendite", k => Pct(k.GrossRentalYieldPercent));
        Kpi("Nettomietrendite", k => Pct(k.NetRentalYieldPercent));
        Kpi("Cash-on-Cash Return", k => Pct(k.CashOnCashReturnPercent));
        Kpi("ROI", k => Pct(k.RoiPercent));
        Kpi("Beleihungsauslauf (LTV)", k => Pct(k.LoanToValuePercent));
        Kpi("Ertragswert (vereinfacht: Jahreskaltmiete x Vervielfältiger)", k => k.EstimatedIncomeValue.HasValue ? Eur(k.EstimatedIncomeValue.Value) : "-");
        sb.AppendLine();
        sb.AppendLine("_Hinweis: Geschätzter Marktwert und Vervielfältiger sind Annahmen des Nutzers. Der Ertragswert ist nur Miete x Faktor " +
            "(ohne Bewirtschaftungskosten, Bodenwert, Restnutzungsdauer). Eine Lücke zwischen beiden Werten hängt direkt vom gewählten " +
            "Faktor ab und ist kein Beleg für Über- oder Unterbewertung (siehe LIESMICH.md)._");
        sb.AppendLine();

        sb.AppendLine("## Einheiten und Mietverhältnisse");
        sb.AppendLine();
        foreach (var unit in p.Units.OrderBy(u => u.Label))
        {
            sb.AppendLine($"### {unit.Label}");
            sb.AppendLine();
            var current = unit.CurrentTenancy;
            // Die Kaltmiete der Einheit ist die des laufenden Mietverhältnisses (einzige Quelle); ohne
            // laufendes Mietverhältnis zählt die Einheit mit 0 (Leerstand oder nicht erfasst).
            sb.AppendLine($"- Fläche: {unit.AreaSqm.ToString("N2", De)} m², Kaltmiete: {Eur(unit.ColdRentMonthly)} / Monat (laut laufendem Mietverhältnis), nicht umlegbare Kosten: {Eur(unit.NonAllocableCostsMonthly)} / Monat");

            if (current is null)
            {
                sb.AppendLine("- Aktuell: kein laufendes Mietverhältnis hinterlegt (Leerstand oder nicht erfasst) - zählt mit 0 € Kaltmiete");
            }
            else
            {
                sb.AppendLine($"- Aktueller Mieter: {current.TenantName} (E-Mail: {current.TenantEmail ?? "-"}, Telefon: {current.TenantPhone ?? "-"})");
                sb.AppendLine($"  - Mietbeginn {D(current.MoveInDate)}, Mietende {(current.MoveOutDate.HasValue ? D(current.MoveOutDate.Value) : "unbefristet")}");
                sb.AppendLine($"  - Kaltmiete {Eur(current.ColdRentMonthly)}, Nebenkostenvorauszahlung {Eur(current.AdvancePaymentMonthly)}, Kaution {Eur(current.SecurityDeposit)}");
                if (!string.IsNullOrWhiteSpace(current.Notes))
                {
                    sb.AppendLine($"  - Notiz: {current.Notes.ReplaceLineEndings(" ")}");
                }
            }

            // Alle übrigen Mietverhältnisse nach ihrem Zustand trennen - "nicht aktuell" heißt nicht
            // automatisch "früher": ein künftiger Nachmieter und ein noch nicht ausgewerteter Upload
            // wären sonst als frühere Mieter beschriftet.
            var today = DateOnly.FromDateTime(DateTime.Today);
            var others = unit.Tenancies.Where(t => t != current).OrderByDescending(t => t.MoveInDate).ToList();
            var future = others.Where(t => !t.IsPendingAnalysis && t.MoveInDate > today).OrderBy(t => t.MoveInDate).ToList();
            var pending = others.Where(t => t.IsPendingAnalysis).ToList();
            var former = others.Where(t => !t.IsPendingAnalysis && t.MoveInDate <= today).ToList();

            if (future.Count > 0)
            {
                sb.AppendLine("- Künftige Mietverhältnisse (Einzug liegt in der Zukunft, zählen noch nicht zur Kaltmiete):");
                foreach (var tenancy in future)
                {
                    sb.AppendLine($"  - {tenancy.TenantName}: ab {D(tenancy.MoveInDate)}, Mietende {(tenancy.MoveOutDate.HasValue ? D(tenancy.MoveOutDate.Value) : "unbefristet")}, Kaltmiete {Eur(tenancy.ColdRentMonthly)}");
                }
            }

            if (pending.Count > 0)
            {
                sb.AppendLine("- Hochgeladene Mietverträge, noch nicht ausgewertet (zählen weder als Mieter noch zur Kaltmiete):");
                foreach (var tenancy in pending)
                {
                    sb.AppendLine($"  - Datei: {tenancy.PdfFileName ?? "-"}");
                }
            }

            if (former.Count > 0)
            {
                sb.AppendLine("- Frühere Mietverhältnisse:");
                foreach (var tenancy in former)
                {
                    sb.AppendLine($"  - {tenancy.TenantName}: {D(tenancy.MoveInDate)} bis {D(tenancy.MoveOutDate)}, Kaltmiete {Eur(tenancy.ColdRentMonthly)}");
                }
            }

            sb.AppendLine();
        }

        sb.AppendLine("## Finanzierung");
        sb.AppendLine();
        if (p.Financings.Count == 0)
        {
            sb.AppendLine("Keine Fremdfinanzierung erfasst.");
        }
        foreach (var financing in p.Financings.OrderBy(f => f.BankName))
        {
            sb.AppendLine($"- **{financing.BankName}** ({FinancingService.LoanTypeLabels[financing.LoanType]}): ursprünglich {Eur(financing.OriginalLoanAmount)}, Restschuld {Eur(financing.CurrentRemainingDebt)}, Zins {Pct(financing.InterestRatePercent)}, anfängliche Tilgung {Pct(financing.InitialRepaymentRatePercent)}, Rate {Eur(financing.MonthlyPayment)} / Monat, Zinsbindung bis {D(financing.FixedInterestEndDate)}");
            foreach (var vehicle in financing.RepaymentVehicles)
            {
                sb.AppendLine($"  - Tilgungsersatzmittel {vehicle.ProductName}: Ziel {Eur(vehicle.TargetAmount)}, aktuell {Eur(vehicle.CurrentValue)}, Beitrag {Eur(vehicle.MonthlyContribution)} / Monat, Laufzeitende {D(vehicle.ContractEndDate)}");
            }
        }
        sb.AppendLine();

        sb.AppendLine("## Versicherungen");
        sb.AppendLine();
        if (bundle.Policies.Count == 0)
        {
            sb.AppendLine("Keine Police erfasst.");
        }
        foreach (var policy in bundle.Policies)
        {
            sb.AppendLine($"- {InsuranceService.CategoryDisplayNames[policy.Category]}: {policy.Provider ?? "-"}, Police-Nr. {policy.PolicyNumber ?? "-"}, Jahresbeitrag {Eur(policy.AnnualPremium)}, Laufzeit {D(policy.StartDate)} bis {D(policy.ExpirationDate)}");
        }
        sb.AppendLine();

        sb.AppendLine("## Nebenkostenabrechnungen");
        sb.AppendLine();
        if (bundle.Statements.Count == 0)
        {
            sb.AppendLine("Keine Abrechnung erfasst.");
        }
        foreach (var statement in bundle.Statements.OrderByDescending(s => s.Year).ThenBy(s => s.PropertyUnit?.Label))
        {
            sb.AppendLine($"### {statement.Year} - {statement.PropertyUnit?.Label ?? "Gesamtobjekt"}");
            sb.AppendLine();
            sb.AppendLine($"- Gesamtkosten: {Eur(statement.TotalCosts)} für {statement.PeriodMonths.ToString("0.##", De)} Monate, {(statement.IsCompleted ? "abgeschlossen" : "noch nicht abgeschlossen")}");
            if (statement.Items.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("| Kategorie | Position | Betrag |");
                sb.AppendLine("|---|---|---|");
                foreach (var item in statement.Items.OrderBy(i => i.Category))
                {
                    sb.AppendLine($"| {UtilityCostCatalog.CategoryDisplayNames[item.Category]} | {Cell(item.Description)} | {Eur(item.Amount)} |");
                }
            }

            sb.AppendLine();
        }

        sb.AppendLine("## Renovierungen");
        sb.AppendLine();
        if (bundle.Renovations.Count == 0)
        {
            sb.AppendLine("Keine Renovierungsprojekte erfasst.");
        }
        foreach (var renovation in bundle.Renovations.OrderBy(r => r.StartDate))
        {
            sb.AppendLine($"- **{renovation.Name}** ({RenovationAnalyticsService.CategoryDisplayNames[renovation.Category]}, {RenovationAnalyticsService.StatusDisplayNames[renovation.Status]}): {D(renovation.StartDate)} bis {D(renovation.EndDate)}, geplant {Eur(renovation.PlannedTotalCost)}, Ist {Eur(renovation.ActualTotalCost)}{(string.IsNullOrWhiteSpace(renovation.Notes) ? "" : $" - {renovation.Notes.ReplaceLineEndings(" ")}")}");
        }
        sb.AppendLine();

        sb.AppendLine("## Logbuch");
        sb.AppendLine();
        sb.AppendLine("Enthält auch Notizen, die Armin Asset im Gespräch festgehalten hat (Kennzeichnung \"von Armin\").");
        sb.AppendLine();
        if (bundle.Log.Count == 0)
        {
            sb.AppendLine("Keine Einträge.");
        }
        else
        {
            sb.AppendLine("| Zeitpunkt | Kategorie | Was | Bezug | Kosten |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var item in bundle.Log)
            {
                var category = item.Source == PropertyLogItemSource.Renovation
                    ? "Renovierung"
                    : PropertyLogService.CategoryDisplayNames[item.Category ?? LogEntryCategory.Sonstiges] + (item.IsFromArminAsset ? " (von Armin)" : "");
                sb.AppendLine($"| {Cell(item.DateLabel)} | {category} | {Cell(item.Description)} | {Cell(item.UnitLabel ?? "Ganzes Objekt")} | {(item.Cost.HasValue ? Eur(item.Cost.Value) : "-")} |");
            }
        }
        sb.AppendLine();

        sb.AppendLine("## Dokumente");
        sb.AppendLine();
        var existing = bundle.Mirrors.Where(m => m.Exists).ToList();
        if (existing.Count == 0)
        {
            sb.AppendLine("Keine Dokumente hinterlegt.");
        }
        else if (!_options.MirrorDocuments)
        {
            sb.AppendLine("In Immomanager hinterlegt. Die Dateien selbst sind nicht Teil dieses Exports, sie liegen im strukturierten OneDrive-Ordner (dort nach Name oder Inhalt suchen).");
            sb.AppendLine();
        }
        foreach (var mirror in existing)
        {
            sb.AppendLine(_options.MirrorDocuments
                ? $"- {mirror.Description}: `{mirror.TargetRelativePath}` (relativ zum Export-Ordner)"
                : $"- {mirror.Description}{(string.IsNullOrWhiteSpace(mirror.OriginalFileName) ? "" : $" (Datei: {mirror.OriginalFileName})")}");
        }

        return sb.ToString();
    }

    private string RenderOverview(List<PropertyBundle> bundles, List<Property> properties, DateTime nowLocal)
    {
        var portfolio = _kpiService.CalculatePortfolio(properties, ViewMode.MyShare);
        var sb = new StringBuilder();
        sb.AppendLine("# Portfolio-Übersicht");
        sb.AppendLine();
        sb.AppendLine($"_Stand: {nowLocal:dd.MM.yyyy HH:mm} - Export aus Immomanager, nur lesen. Alle Werte anteilig nach Beteiligungsquote (\"Mein Anteil\")._");
        sb.AppendLine();
        sb.AppendLine($"- Anzahl Immobilien: {portfolio.PropertyCount}");
        sb.AppendLine($"- Gesamtinvestition: {Eur(portfolio.TotalInvestment)}");
        sb.AppendLine($"- Marktwert gesamt: {Eur(portfolio.TotalMarketValue)}");
        sb.AppendLine($"- Restschuld gesamt: {Eur(portfolio.TotalRemainingDebt)}");
        sb.AppendLine($"- Eigenkapital-Spiegel (Marktwert minus Restschuld): {Eur(portfolio.TotalEquity)}");
        sb.AppendLine($"- Kaltmiete: {Eur(portfolio.ColdRentMonthly)} / Monat");
        sb.AppendLine($"- Cashflow vor Steuern: {Eur(portfolio.CashflowMonthly)} / Monat");
        sb.AppendLine($"- Bruttomietrendite: {Pct(portfolio.GrossRentalYieldPercent)}");
        sb.AppendLine();
        sb.AppendLine("| Objekt | Adresse | Marktwert (Schätzung) | Ertragswert (vereinfacht) | Vervielfältiger | Kaltmiete / Monat | Cashflow / Monat | Restschuld | Bruttorendite | LTV | Details |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var bundle in bundles)
        {
            var k = bundle.KpiMine;
            var incomeValue = k.EstimatedIncomeValue.HasValue ? Eur(k.EstimatedIncomeValue.Value) : "-";
            var multiplier = bundle.Property.IncomeMultiplier is { } m ? m.ToString("N1", De) : "-";
            sb.AppendLine($"| {Cell(bundle.Property.Name)} | {Cell(bundle.Property.Address)} | {Eur(k.CurrentMarketValue)} | {incomeValue} | {multiplier} | {Eur(k.ColdRentMonthly)} | {Eur(k.CashflowMonthly)} | {Eur(k.TotalRemainingDebt)} | {Pct(k.GrossRentalYieldPercent)} | {Pct(k.LoanToValuePercent)} | `objekte/{bundle.FileBaseName}.md` |");
        }

        sb.AppendLine();
        sb.AppendLine("_Marktwert (Schätzung) und Vervielfältiger sind Annahmen des Nutzers; der Ertragswert ist nur Jahreskaltmiete x Vervielfältiger. " +
            "Die Lücke zwischen beiden hängt direkt vom gewählten Faktor ab und ist kein Beleg für Über- oder Unterbewertung (siehe LIESMICH.md)._");

        return sb.ToString();
    }

    private static string RenderObjectsCsv(List<PropertyBundle> bundles)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CsvRow("Objekt", "Adresse", "Eigentuemer", "Beteiligung %", "Kaufpreis", "Marktwert", "Kaltmiete/Monat", "Cashflow/Monat", "Restschuld", "Bruttorendite %", "Nettorendite %", "LTV %", "Vervielfaeltiger", "Ertragswert"));
        foreach (var bundle in bundles)
        {
            var p = bundle.Property;
            var k = bundle.KpiTotal;
            sb.AppendLine(CsvRow(p.Name, p.Address, p.Owner?.Name, Num(p.OwnershipSharePercent), Num(p.PurchasePrice), Num(p.CurrentMarketValue),
                Num(k.ColdRentMonthly), Num(k.CashflowMonthly), Num(k.TotalRemainingDebt), Num(k.GrossRentalYieldPercent), Num(k.NetRentalYieldPercent), Num(k.LoanToValuePercent),
                p.IncomeMultiplier.HasValue ? Num(p.IncomeMultiplier.Value) : "",
                k.EstimatedIncomeValue.HasValue ? Num(k.EstimatedIncomeValue.Value) : ""));
        }

        return sb.ToString();
    }

    private static string RenderTenanciesCsv(List<PropertyBundle> bundles)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CsvRow("Objekt", "Einheit", "Mieter", "E-Mail", "Telefon", "Einzug", "Auszug", "Aktuell", "Kaltmiete", "NK-Vorauszahlung", "Kaution"));
        foreach (var bundle in bundles)
        {
            foreach (var unit in bundle.Property.Units.OrderBy(u => u.Label))
            {
                foreach (var tenancy in unit.Tenancies.OrderByDescending(t => t.MoveInDate))
                {
                    sb.AppendLine(CsvRow(bundle.Property.Name, unit.Label, tenancy.TenantName, tenancy.TenantEmail, tenancy.TenantPhone,
                        D(tenancy.MoveInDate), tenancy.MoveOutDate.HasValue ? D(tenancy.MoveOutDate.Value) : "", tenancy.IsCurrent ? "ja" : "nein",
                        Num(tenancy.ColdRentMonthly), Num(tenancy.AdvancePaymentMonthly), tenancy.SecurityDeposit.HasValue ? Num(tenancy.SecurityDeposit.Value) : ""));
                }
            }
        }

        return sb.ToString();
    }

    private static string RenderReadme(int objectCount, int documentCount, bool mirrorDocuments, DateTime nowLocal)
    {
        // Mehrzeilige Einträge mit festem Einzug (zwei Leerzeichen), weil die Zeilen unten im Raw-String
        // beim Einsetzen nicht mehr eingerückt werden.
        var documentSummary = mirrorDocuments
            ? $"{documentCount} Dokumente"
            : $"{documentCount} hinterlegte Dokumente (nur aufgelistet, nicht kopiert)";
        var documentFiles = mirrorDocuments
            ? "- `dokumente/<Objektname>/...` - Kopien der in Immomanager hochgeladenen Unterlagen (Mietverträge,\n"
                + "  Nebenkostenabrechnungen, Policen, Objekt- und Einheitsdokumente)"
            : "- Dokumente: nicht Teil dieses Exports. Die Objekt-Dateien listen unter \"Dokumente\" nur, was in Immomanager\n"
                + "  hinterlegt ist; die Dateien selbst liegen im strukturierten OneDrive-Ordner des Nutzers (dort suchen).";

        return $"""
        # Immomanager-Export für Claude

        Stand: {nowLocal:dd.MM.yyyy HH:mm} - {objectCount} Objekte, {documentSummary}.

        Dieser Ordner wird täglich automatisch von der App "Immomanager" (private Immobilienverwaltung)
        geschrieben. Er ist ein **Lese-Stand**: nichts hier drin ändern oder ergänzen, es wird beim nächsten
        Export überschrieben bzw. aufgeräumt. Änderungen an den Daten passieren nur in Immomanager selbst.

        ## Dateien

        - `uebersicht.md` - Einstieg: Portfolio-Summen und eine Zeile je Objekt (Werte "Mein Anteil")
        - `objekte/<Objektname>.md` - alles zu einem Objekt: Stammdaten, Kennzahlen, Einheiten und Mieter
          (inkl. Kontaktdaten), Finanzierung, Versicherungen, Nebenkostenabrechnungen, Renovierungen,
          Logbuch, Dokumentenliste
        - `objekte.csv` / `mietverhaeltnisse.csv` - dieselben Daten tabellarisch (Semikolon, deutsches Zahlenformat)
        {documentFiles}
        - `immomanager-snapshot.db` - konsistente Kopie der SQLite-Datenbank für Auswertungen mit `sqlite3`
          (nur lesen, nie in die Live-Datenbank schreiben)

        ## Hinweise zur Interpretation

        - Kennzahlen (Cashflow, Renditen, LTV, Ertragswert) werden von der App berechnet und stehen in den
          Objekt-Dateien, **nicht** als Spalten in der Datenbank. Formeln:
          - Bruttomietrendite = Jahreskaltmiete / Kaufpreis
          - Nettomietrendite = (Jahreskaltmiete - nicht umlegbare Kosten) / Gesamtkaufkosten
          - Cashflow vor Steuern = Kaltmiete - nicht umlegbare Kosten - Kapitaldienst
          - Cash-on-Cash Return = Jahres-Cashflow / eingesetztes Eigenkapital
          - LTV = Restschuld / Marktwert
          - Ertragswert (vereinfacht) = Jahresnettokaltmiete x Vervielfältiger
        - **Marktwert und Ertragswert sind Annahmen des Nutzers, keine Marktbeobachtung.** Der Marktwert ist eine
          vom Nutzer geschätzte Zahl ("Geschätzter Marktwert"). Der Ertragswert ist nur Jahresnettokaltmiete x
          Vervielfältiger, und der Vervielfältiger ist ein vom Nutzer je Objekt frei gewählter Faktor (steht in
          `uebersicht.md`, `objekte.csv` und unter "Ertragswert-Vervielfältiger" in der Objekt-Datei).
          Bewirtschaftungskosten, Bodenwert und Restnutzungsdauer fließen nicht ein. Eine Lücke zwischen Marktwert
          und Ertragswert zeigt deshalb nur, dass zwei Annahmen auseinanderliegen, und hängt direkt vom gewählten
          Faktor ab (anderer Faktor = andere Lücke). Sie ist **kein Beleg**, dass der Marktwert zu hoch oder zu
          niedrig ist. Bewerte den Marktwert nur mit diesem Vorbehalt und nenne den verwendeten Vervielfältiger
          dazu. Ein LTV auf Basis des Ertragswerts ist ebenfalls nur eine Rechenvariante.
        - Die Kaltmiete wird nur im Mietverhältnis gepflegt (`mietverhaeltnisse.csv`, Tabelle `Tenancies`).
          Die Kaltmiete einer Einheit ist die des laufenden Mietverhältnisses, die eines Objekts die Summe über
          alle Einheiten. Einheiten ohne laufendes Mietverhältnis (Leerstand oder nicht erfasst) zählen mit
          0 € - Cashflow und Renditen zeigen damit den tatsächlichen Stand, nicht eine Soll-Miete.
        - "Mein Anteil" berücksichtigt die Beteiligungsquote, "Gesamtobjekt" nicht. Manche Objekte werden über
          eine Gesellschaft gehalten (siehe "Eigentümer" in der Objekt-Datei).
        - Mieterdaten sind personenbezogen: nur für die Verwaltung der Objekte verwenden und nicht
          weitergeben.
        """;
    }

    // ---- Hilfsfunktionen --------------------------------------------------------------------------

    private static string Eur(decimal value) => value.ToString("N2", De) + " €";

    private static string Eur(decimal? value) => value.HasValue ? Eur(value.Value) : "-";

    private static string Pct(decimal value) => value.ToString("N2", De) + " %";

    private static string D(DateOnly value) => value.ToString("dd.MM.yyyy", De);

    private static string D(DateOnly? value) => value.HasValue ? D(value.Value) : "-";

    private static string Num(decimal value) => value.ToString("0.00", De);

    private static string Cell(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.Replace("|", "/").ReplaceLineEndings(" ");

    private static string CsvRow(params string?[] cells) => string.Join(';', cells.Select(CsvCell));

    private static string CsvCell(string? cell)
    {
        cell ??= string.Empty;
        return cell.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell;
    }

    private static string SafeName(string value, string fallback)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(c => invalid.Contains(c) || c is '/' or '\\' ? '_' : c).ToArray())
            .Trim().TrimEnd('.');
        if (cleaned.Length > 80)
        {
            cleaned = cleaned[..80].TrimEnd();
        }

        return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
    }

    /// <summary>Wie <see cref="SafeName"/>, kürzt aber nur den Namensteil und behält die Dateiendung (sonst
    /// würde ein langer, aus Einheit/Mieter/Originalname zusammengesetzter Name seine ".pdf"-Endung verlieren).</summary>
    private static string SafeFileName(string value)
    {
        var extension = Path.GetExtension(value);
        if (extension.Length > 6 || extension.Contains(' '))
        {
            extension = string.Empty;
        }

        return SafeName(value[..^extension.Length], "Datei") + extension;
    }

    private static string UniqueName(string name, int id, HashSet<string> used)
    {
        var candidate = used.Contains(name) ? $"{name}_{id}" : name;
        used.Add(candidate);
        return candidate;
    }

    private static string UniqueTarget(string relativePath, HashSet<string> used)
    {
        var candidate = relativePath;
        var counter = 2;
        while (!used.Add(candidate))
        {
            var extension = Path.GetExtension(relativePath);
            candidate = $"{relativePath[..^extension.Length]}_{counter++}{extension}";
        }

        return candidate;
    }

    private sealed record RelatedData(
        List<InsurancePolicy> Policies,
        List<UtilityStatement> Statements,
        List<RenovationProject> Renovations,
        List<PropertyDocument> PropertyDocuments,
        List<UnitDocument> UnitDocuments);

    private sealed record PropertyBundle(
        Property Property,
        string FileBaseName,
        PropertyKpi KpiTotal,
        PropertyKpi KpiMine,
        List<InsurancePolicy> Policies,
        List<UtilityStatement> Statements,
        List<RenovationProject> Renovations,
        List<PropertyLogItem> Log,
        List<MirrorEntry> Mirrors);

    private sealed class MirrorEntry(string description, string sourceRelativePath, string targetRelativePath, string? originalFileName)
    {
        public string Description { get; } = description;

        public string SourceRelativePath { get; } = sourceRelativePath;

        public string TargetRelativePath { get; } = targetRelativePath;

        public string? OriginalFileName { get; } = originalFileName;

        public bool Exists { get; set; }
    }
}
