using System.Globalization;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Immomanager.Web.Models;
using Microsoft.Extensions.Options;

namespace Immomanager.Web.Services;

/// <summary>Agenten-Service für "Armin Asset": führt die Claude-Tool-Calling-Schleife aus und stellt
/// den Tools lesenden/erzeugenden Zugriff auf Portfolio-Daten und Dokument-Generatoren bereit.
/// Bewusst kein "beliebiges SQL ausführen"-Tool (Injection-/Prompt-Injection-Risiko) - stattdessen
/// liefert QueryDatabaseCustom einen sicheren, schreibgeschützten Daten-Snapshot über EF Core.</summary>
public class ArminAssetAgentService : IArminAssetAgentService
{
    private const int MaxToolIterations = 6;

    private const string SystemPrompt =
        "Du bist 'Armin Asset', der persönliche KI-Immobilien-Analyst und Co-Pilot des Investors. " +
        "Du hast Zugriff auf reale Objektdaten, Finanzierungen, Fotos und Versicherungspolicen in der " +
        "SQLite-Datenbank. Antworte immer präzise, kaufmännisch fundiert, freundlich und auf den Punkt. " +
        "Wenn der Nutzer nach Berichten, Exposés oder spezifischen Zahlen fragt, nutze deine " +
        "bereitgestellten Tools, anstatt zu raten. Alle Geldbeträge sind in Euro. Wenn ein Tool ein " +
        "Dokument erzeugt hat, erwähne kurz, dass es zum Download bereitsteht - der Download-Link wird " +
        "dem Nutzer separat von der Anwendung angezeigt, du musst ihn nicht selbst ausgeben. " +
        "Nach einer Policen-Analyse (analyze_insurance_policy_pdf) fasse strukturiert zusammen, " +
        "z. B. \"Ich habe die Gebäudeversicherung für [Objekt] analysiert. Jahresbeitrag: X € " +
        "(Y €/Einheit -> Benchmark: [Grün/Gelb/Rot]).\" und weise explizit auf jede gefundene Lücke hin " +
        "(z. B. \"Achtung: Der Baustein Elementarschäden wurde in der Police NICHT gefunden!\"). Für " +
        "Detailfragen zum genauen Vertragstext einer Police (z. B. Selbstbeteiligung, Ausschlüsse) rufe " +
        "das Tool erneut auf statt zu raten - es liest die hinterlegte PDF jedes Mal frisch ein. " +
        "Nach einer Nebenkosten-Analyse (analyze_utility_statement_pdf) fasse strukturiert zusammen, " +
        "z. B. \"Ich habe die Nebenkostenabrechnung für [Objekt bzw. Einheit] für das Jahr [Jahr] " +
        "analysiert. Gesamtkosten: X €. Kosten pro m²: X,XX €/m²/Monat. Größter Kostenfaktor: " +
        "[Kategorie] mit Z €. Die Kostenpositionen wurden gespeichert und die Dashboard-Erinnerung für " +
        "[Jahr] wurde aufgelöst.\" Frag der Nutzer nach der realistischen NK-Vorauszahlung für eine " +
        "Einheit (z. B. bei Neuvermietung), nutze die zuletzt bekannten €/m²/Monat dieser Einheit aus " +
        "analyze_utility_statement_pdf bzw. get_property_details als Grundlage. Für Mietverhältnisse: " +
        "Wenn ein Mietvertrag zu einer Einheit hochgeladen wurde " +
        "(erkennbar an \"mietvertragHochgeladen\": true bei get_property_details oder auf Nutzeranfrage), " +
        "lies ihn mit analyze_lease_pdf ein und lege das Mietverhältnis automatisch an; fasse danach " +
        "Mieter, Zeitraum und Miete zusammen. Für Detailfragen zum Vertragstext (z. B. Kündigungsfristen, " +
        "Kleintierklausel) rufe das Tool erneut auf - es liest die hinterlegte PDF jedes Mal frisch ein. " +
        "Mit generate_tenant_letter kannst du Mahnungen, einfache Anschreiben oder Kündigungsentwürfe an " +
        "einen Mieter erzeugen: formuliere \"subject\" und \"bodyText\" selbst passend zum Anlass (den du " +
        "aus dem Gespräch mit dem Nutzer kennst, z. B. konkrete Beträge, Fristen, Gründe) - das Tool " +
        "kümmert sich nur um Absender-/Empfänger-/Objektbezug und die PDF-Formatierung. Frage nach " +
        "Absendername/-adresse, falls dir diese noch nicht bekannt sind. WICHTIG: Diese Schreiben sind " +
        "IMMER nur Entwürfe zur Prüfung durch den Nutzer - du versendest oder verschickst NICHTS selbst " +
        "(weder postalisch noch per E-Mail), und weise besonders bei Kündigungen darauf hin, dass eine " +
        "rechtliche Prüfung vor Versand empfehlenswert ist (Kündigungsfristen und -gründe im deutschen " +
        "Mietrecht sind streng geregelt).";

    private readonly IOptionsMonitor<AnthropicOptions> _optionsMonitor;
    private readonly IPropertyService _propertyService;
    private readonly IRenovationService _renovationService;
    private readonly IDealCalculationService _dealService;
    private readonly IPropertyImageService _imageService;
    private readonly KpiCalculationService _kpiService;
    private readonly IExposePdfGenerator _pdfGenerator;
    private readonly IPropertyPowerPointGenerator _pptGenerator;
    private readonly IInsuranceService _insuranceService;
    private readonly IExposeParserService _pdfTextExtractor;
    private readonly IInsurancePolicyAnalysisService _policyAnalysisService;
    private readonly IUtilityService _utilityService;
    private readonly IUtilityStatementAnalysisService _utilityAnalysisService;
    private readonly ITenancyService _tenancyService;
    private readonly ILeaseAnalysisService _leaseAnalysisService;
    private readonly ITenantLetterPdfGenerator _letterGenerator;
    private readonly StorageOptions _storageOptions;
    private readonly ILogger<ArminAssetAgentService> _logger;

    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    private static readonly JsonSerializerOptions ToolResultJsonOptions = new() { WriteIndented = false };

    public ArminAssetAgentService(
        IOptionsMonitor<AnthropicOptions> optionsMonitor,
        IPropertyService propertyService,
        IRenovationService renovationService,
        IDealCalculationService dealService,
        IPropertyImageService imageService,
        KpiCalculationService kpiService,
        IExposePdfGenerator pdfGenerator,
        IPropertyPowerPointGenerator pptGenerator,
        IInsuranceService insuranceService,
        IExposeParserService pdfTextExtractor,
        IInsurancePolicyAnalysisService policyAnalysisService,
        IUtilityService utilityService,
        IUtilityStatementAnalysisService utilityAnalysisService,
        ITenancyService tenancyService,
        ILeaseAnalysisService leaseAnalysisService,
        ITenantLetterPdfGenerator letterGenerator,
        StorageOptions storageOptions,
        ILogger<ArminAssetAgentService> logger)
    {
        _optionsMonitor = optionsMonitor;
        _propertyService = propertyService;
        _renovationService = renovationService;
        _dealService = dealService;
        _imageService = imageService;
        _kpiService = kpiService;
        _pdfGenerator = pdfGenerator;
        _pptGenerator = pptGenerator;
        _insuranceService = insuranceService;
        _pdfTextExtractor = pdfTextExtractor;
        _policyAnalysisService = policyAnalysisService;
        _utilityService = utilityService;
        _utilityAnalysisService = utilityAnalysisService;
        _tenancyService = tenancyService;
        _leaseAnalysisService = leaseAnalysisService;
        _letterGenerator = letterGenerator;
        _storageOptions = storageOptions;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_optionsMonitor.CurrentValue.ApiKey);

    public async Task<ArminAgentTurnResult> SendMessageAsync(
        List<MessageParam> conversation,
        string userMessage,
        Func<string, Task> onStatusUpdate,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new ArminAgentTurnResult(
                "Kein Anthropic API-Key hinterlegt. Bitte unter \"Einstellungen\" in der Navigation konfigurieren.", null, null);
        }

        var options = _optionsMonitor.CurrentValue;
        conversation.Add(new MessageParam { Role = Role.User, Content = userMessage });

        AnthropicClient client = new() { ApiKey = options.ApiKey };
        string? downloadFileName = null;
        string? downloadUrl = null;

        for (var iteration = 0; iteration < MaxToolIterations; iteration++)
        {
            Message response;
            try
            {
                response = await client.Messages.Create(new MessageCreateParams
                {
                    Model = options.Model,
                    MaxTokens = 2000,
                    System = SystemPrompt,
                    Tools = BuildTools(),
                    Messages = conversation,
                }, cancellationToken);
            }
            catch (AnthropicUnauthorizedException ex)
            {
                _logger.LogError(ex, "Anthropic API-Key ungültig bei Armin-Asset-Anfrage.");
                return new ArminAgentTurnResult("Der Anthropic API-Key ist ungültig oder abgelaufen.", null, null);
            }
            catch (AnthropicRateLimitException ex)
            {
                _logger.LogWarning(ex, "Anthropic-Anfragelimit bei Armin-Asset-Anfrage erreicht.");
                return new ArminAgentTurnResult("Anthropic-Anfragelimit erreicht - bitte kurz warten und erneut versuchen.", null, null);
            }
            catch (AnthropicApiException ex)
            {
                _logger.LogError(ex, "Anthropic-API-Fehler bei Armin-Asset-Anfrage.");
                return new ArminAgentTurnResult($"Fehler bei der Anfrage an Armin: {ex.Message}", null, null);
            }

            var assistantBlocks = new List<ContentBlockParam>();
            var toolResultBlocks = new List<ContentBlockParam>();
            string? textThisTurn = null;

            foreach (var block in response.Content)
            {
                switch (block.Value)
                {
                    case TextBlock text:
                        assistantBlocks.Add(new TextBlockParam { Text = text.Text });
                        textThisTurn = text.Text;
                        break;

                    case ToolUseBlock toolUse:
                        assistantBlocks.Add(new ToolUseBlockParam { ID = toolUse.ID, Name = toolUse.Name, Input = toolUse.Input });
                        await onStatusUpdate(ToolStatusLabel(toolUse.Name));

                        string resultText;
                        try
                        {
                            var (text, fileInfo) = await ExecuteToolAsync(toolUse.Name, toolUse.Input, cancellationToken);
                            resultText = text;
                            if (fileInfo is { } file)
                            {
                                downloadFileName = file.FileName;
                                downloadUrl = file.Url;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Armin-Asset-Werkzeug {ToolName} fehlgeschlagen.", toolUse.Name);
                            resultText = JsonSerializer.Serialize(new { error = ex.Message }, ToolResultJsonOptions);
                        }

                        toolResultBlocks.Add(new ToolResultBlockParam { ToolUseID = toolUse.ID, Content = resultText });
                        break;
                }
            }

            conversation.Add(new MessageParam { Role = Role.Assistant, Content = assistantBlocks });

            if (response.StopReason != "tool_use")
            {
                return new ArminAgentTurnResult(textThisTurn ?? "(Armin hat keine Antwort geliefert.)", downloadFileName, downloadUrl);
            }

            conversation.Add(new MessageParam { Role = Role.User, Content = toolResultBlocks });
        }

        return new ArminAgentTurnResult(
            "Ich konnte die Anfrage nach mehreren Schritten nicht abschließen - kannst du sie etwas genauer formulieren?",
            downloadFileName, downloadUrl);
    }

    private static string ToolStatusLabel(string toolName) => toolName switch
    {
        "get_portfolio_summary" => "Armin ruft die Portfolio-Übersicht ab...",
        "get_property_details" => "Armin lädt die Objektdetails...",
        "query_database_custom" => "Armin durchsucht die Datenbank...",
        "generate_expose_pdf" => "Armin erstellt das PDF-Exposé...",
        "generate_power_point" => "Armin erstellt die PowerPoint-Präsentation...",
        "analyze_insurance_policy_pdf" => "Armin liest die Versicherungspolice und prüft die Checkliste...",
        "analyze_utility_statement_pdf" => "Armin liest die Nebenkostenabrechnung und wertet die Positionen aus...",
        "analyze_lease_pdf" => "Armin liest den Mietvertrag und legt das Mietverhältnis an...",
        "generate_tenant_letter" => "Armin erstellt den Schreiben-Entwurf...",
        _ => $"Armin führt Werkzeug \"{toolName}\" aus...",
    };

    private async Task<(string ResultText, (string FileName, string Url)? FileInfo)> ExecuteToolAsync(
        string toolName, IReadOnlyDictionary<string, JsonElement> input, CancellationToken cancellationToken)
    {
        switch (toolName)
        {
            case "get_portfolio_summary":
                return (await GetPortfolioSummaryAsync(), null);

            case "get_property_details":
                return (await GetPropertyDetailsAsync(GetPropertyId(input)), null);

            case "query_database_custom":
                return (await QueryDatabaseCustomAsync(), null);

            case "generate_expose_pdf":
            {
                var (fileName, url) = await _pdfGenerator.GenerateAsync(GetPropertyId(input), cancellationToken);
                return ($"PDF-Exposé \"{fileName}\" wurde erfolgreich erstellt.", (fileName, url));
            }

            case "generate_power_point":
            {
                var (fileName, url) = await _pptGenerator.GenerateAsync(GetPropertyId(input), cancellationToken);
                return ($"PowerPoint-Präsentation \"{fileName}\" wurde erfolgreich erstellt.", (fileName, url));
            }

            case "analyze_insurance_policy_pdf":
                return (await AnalyzeInsurancePolicyPdfAsync(GetPropertyId(input), GetInsuranceCategory(input), cancellationToken), null);

            case "analyze_utility_statement_pdf":
                return (await AnalyzeUtilityStatementPdfAsync(GetPropertyId(input), GetOptionalUnitId(input), GetYear(input), cancellationToken), null);

            case "analyze_lease_pdf":
                return (await AnalyzeLeasePdfAsync(GetPropertyId(input), GetUnitId(input), cancellationToken), null);

            case "generate_tenant_letter":
            {
                var (fileName, url) = await GenerateTenantLetterAsync(input, cancellationToken);
                return ($"Schreiben \"{fileName}\" wurde als Entwurf erstellt.", (fileName, url));
            }

            default:
                return ($"Unbekanntes Werkzeug: {toolName}", null);
        }
    }

    private static int GetPropertyId(IReadOnlyDictionary<string, JsonElement> input) =>
        input.TryGetValue("propertyId", out var value) ? value.GetInt32() : throw new InvalidOperationException("propertyId fehlt.");

    private static InsuranceCategory GetInsuranceCategory(IReadOnlyDictionary<string, JsonElement> input)
    {
        if (!input.TryGetValue("insuranceCategory", out var value))
        {
            throw new InvalidOperationException("insuranceCategory fehlt.");
        }

        return Enum.TryParse<InsuranceCategory>(value.GetString(), out var category)
            ? category
            : throw new InvalidOperationException($"Unbekannte insuranceCategory: {value.GetString()}");
    }

    private static int GetYear(IReadOnlyDictionary<string, JsonElement> input) =>
        input.TryGetValue("year", out var value) ? value.GetInt32() : throw new InvalidOperationException("year fehlt.");

    private static int GetUnitId(IReadOnlyDictionary<string, JsonElement> input) =>
        input.TryGetValue("unitId", out var value) ? value.GetInt32() : throw new InvalidOperationException("unitId fehlt.");

    private static int? GetOptionalUnitId(IReadOnlyDictionary<string, JsonElement> input) =>
        input.TryGetValue("unitId", out var value) ? value.GetInt32() : null;

    private static string GetRequiredString(IReadOnlyDictionary<string, JsonElement> input, string key) =>
        input.TryGetValue(key, out var value) ? value.GetString() ?? "" : throw new InvalidOperationException($"{key} fehlt.");

    private static string? GetOptionalString(IReadOnlyDictionary<string, JsonElement> input, string key) =>
        input.TryGetValue(key, out var value) ? value.GetString() : null;

    private async Task<string> GetPortfolioSummaryAsync()
    {
        var properties = await _propertyService.GetAllAsync();
        var portfolio = _kpiService.CalculatePortfolio(properties, ViewMode.MyShare);

        return JsonSerializer.Serialize(new
        {
            hinweis = "Werte anteilig nach Beteiligungsquote (mein Anteil).",
            anzahlImmobilien = portfolio.PropertyCount,
            gesamtmarktwert = portfolio.TotalMarketValue,
            gesamtinvestition = portfolio.TotalInvestment,
            gesamtrestschuld = portfolio.TotalRemainingDebt,
            eigenkapitalSpiegel = portfolio.TotalEquity,
            kaltmieteProMonat = portfolio.ColdRentMonthly,
            cashflowProMonat = portfolio.CashflowMonthly,
            bruttomietrenditeProzent = portfolio.GrossRentalYieldPercent,
            objekte = portfolio.PropertyKpis.Select(k => new { k.PropertyId, k.PropertyName, k.CurrentMarketValue, k.CashflowMonthly }),
        }, ToolResultJsonOptions);
    }

    private async Task<string> GetPropertyDetailsAsync(int propertyId)
    {
        var property = await _propertyService.GetByIdAsync(propertyId);
        if (property is null)
        {
            return JsonSerializer.Serialize(new { error = $"Keine Immobilie mit Id {propertyId} gefunden." });
        }

        var kpi = _kpiService.Calculate(property, ViewMode.MyShare);
        var images = await _imageService.GetByPropertyIdAsync(propertyId);
        var renovations = await _renovationService.GetProjectsByPropertyIdAsync(propertyId);
        var utilityStatements = await _utilityService.GetStatementsForPropertyAsync(propertyId);

        return JsonSerializer.Serialize(new
        {
            property.Id,
            property.Name,
            property.Address,
            property.YearBuilt,
            wohnflaecheM2 = property.LivingAreaSqm,
            property.PurchaseDate,
            property.PurchasePrice,
            gesamtinvestition = property.TotalInvestment,
            beteiligungsquoteProzent = property.OwnershipSharePercent,
            aktuellerMarktwert = property.CurrentMarketValue,
            kaltmieteProMonat = property.CurrentColdRentMonthly,
            nichtUmlegbareKostenProMonat = property.NonAllocableCostsMonthly,
            kennzahlen = new
            {
                kpi.GrossRentalYieldPercent,
                kpi.NetRentalYieldPercent,
                kpi.CashflowMonthly,
                kpi.LoanToValuePercent,
                kpi.RoiPercent,
            },
            finanzierungen = property.Financings.Select(f => new
            {
                f.BankName,
                f.OriginalLoanAmount,
                f.CurrentRemainingDebt,
                f.InterestRatePercent,
                f.MonthlyPayment,
            }),
            renovierungen = renovations.Select(r => new { r.Name, r.Status, r.ActualTotalCost }),
            fotos = images.Select(i => new { i.FileName, url = $"/data-files/{i.RelativePath}" }),
            // "unitId" hier ist die für analyze_lease_pdf/generate_tenant_letter benötigte Id - damit
            // kann Armin eine natürlichsprachliche Einheiten-Referenz (z. B. "die Wohnung im EG") über
            // "label" auflösen, ohne dass der Nutzer eine rohe Datenbank-Id nennen müsste.
            einheiten = property.Units.Select(u =>
            {
                var lastStatement = utilityStatements
                    .Where(s => s.PropertyUnitId == u.Id)
                    .OrderByDescending(s => s.Year)
                    .FirstOrDefault();

                return new
                {
                    unitId = u.Id,
                    label = u.Label,
                    flaecheM2 = u.AreaSqm,
                    kaltmieteProMonat = u.ColdRentMonthly,
                    aktuellerMieter = u.CurrentTenancy == null ? null : new
                    {
                        u.CurrentTenancy.TenantName,
                        u.CurrentTenancy.TenantEmail,
                        u.CurrentTenancy.TenantPhone,
                        u.CurrentTenancy.MoveInDate,
                        u.CurrentTenancy.MoveOutDate,
                        mietvertragHochgeladen = !string.IsNullOrWhiteSpace(u.CurrentTenancy.PdfFilePath),
                    },
                    // Für Fragen zur realistischen NK-Vorauszahlung (z. B. bei Neuvermietung) - die
                    // letzte bekannte Abrechnung dieser Einheit, ohne dass Armin extra danach fragen muss.
                    letzteNkAbrechnung = lastStatement == null ? null : new
                    {
                        jahr = lastStatement.Year,
                        gesamtkosten = lastStatement.TotalCosts,
                        proMonat = lastStatement.TotalCosts / 12,
                        proQmProMonat = u.AreaSqm > 0 ? lastStatement.TotalCosts / u.AreaSqm / 12 : 0,
                    },
                };
            }),
            versicherungen = await GetInsuranceSummaryAsync(property),
        }, ToolResultJsonOptions);
    }

    /// <summary>Kompakte Versicherungsübersicht für get_property_details, damit Armin allgemeine
    /// Fragen (z. B. "welche Versicherungen hat Objekt X, gibt es Lücken?") ohne erneutes Einlesen
    /// der PDF beantworten kann - für Detailfragen zum genauen Vertragstext dient weiterhin
    /// analyze_insurance_policy_pdf, das die hinterlegte PDF jederzeit erneut auswertet.</summary>
    private async Task<object> GetInsuranceSummaryAsync(Property property)
    {
        var policies = await _insuranceService.GetPoliciesAsync(property.Id);
        var checkItems = await _insuranceService.GetCheckItemsAsync(property.Id);

        return Enum.GetValues<InsuranceCategory>().Select(category =>
        {
            var policy = policies.FirstOrDefault(p => p.Category == category);
            var benchmark = _insuranceService.CalculateBenchmark(property, category, policy?.AnnualPremium ?? 0);
            var items = checkItems.Where(c => c.Category == category).ToList();

            return new
            {
                kategorie = InsuranceService.CategoryDisplayNames[category],
                anbieter = policy?.Provider,
                scheinnummer = policy?.PolicyNumber,
                jahresbeitrag = policy?.AnnualPremium,
                ablaufdatum = policy?.ExpirationDate,
                pdfVorhanden = !string.IsNullOrWhiteSpace(policy?.PdfFilePath),
                kostenProEinheitProJahr = benchmark.Status == InsuranceBenchmarkStatus.NichtErmittelbar ? null : (decimal?)benchmark.CostPerUnitAnnual,
                benchmark = benchmark.Status.ToString(),
                luecken = items.Where(i => i.IsCovered == false).Select(i => i.Title),
                nochNichtGeprueft = items.Count(i => i.IsCovered is null),
            };
        }).ToList();
    }

    private async Task<string> QueryDatabaseCustomAsync()
    {
        // Bewusst kein LLM-generiertes SQL (Injection-Risiko) - stattdessen ein sicherer,
        // schreibgeschützter Gesamt-Snapshot über EF Core, aus dem die KI selbst die passenden
        // Informationen herausliest.
        var properties = await _propertyService.GetAllAsync();
        var deals = await _dealService.GetAllAsync();

        return JsonSerializer.Serialize(new
        {
            immobilien = properties.Select(p => new
            {
                p.Id,
                p.Name,
                p.Address,
                p.YearBuilt,
                p.PurchasePrice,
                p.CurrentMarketValue,
                p.CurrentColdRentMonthly,
                anzahlDarlehen = p.Financings.Count,
                gesamtrestschuld = p.Financings.Sum(f => f.CurrentRemainingDebt),
            }),
            ankaufspruefungen = deals.Select(d => new { d.Id, d.Name, d.PurchasePrice, d.PropertyId, Verknuepft = d.Property?.Name }),
        }, ToolResultJsonOptions);
    }

    /// <summary>Liest die für Immobilie+Kategorie hinterlegte Policen-PDF vom Datenträger (jedes Mal
    /// frisch, nicht nur beim ersten Hochladen) und lässt sie per Claude analysieren. Bewusst nur
    /// propertyId+Kategorie als Tool-Eingabe (kein vom Modell frei wählbarer Dateipfad) - der
    /// tatsächliche Pfad wird serverseitig aus der Datenbank aufgelöst.</summary>
    private async Task<string> AnalyzeInsurancePolicyPdfAsync(int propertyId, InsuranceCategory category, CancellationToken cancellationToken)
    {
        var property = await _propertyService.GetByIdAsync(propertyId);
        if (property is null)
        {
            return JsonSerializer.Serialize(new { error = $"Keine Immobilie mit Id {propertyId} gefunden." }, ToolResultJsonOptions);
        }

        var categoryLabel = InsuranceService.CategoryDisplayNames[category];
        var policy = await _insuranceService.GetPolicyAsync(propertyId, category);
        if (policy is null || string.IsNullOrWhiteSpace(policy.PdfFilePath))
        {
            return JsonSerializer.Serialize(new
            {
                error = $"Für \"{property.Name}\" wurde noch keine {categoryLabel}-Police als PDF hochgeladen. " +
                    "Bitte zuerst im Tab \"Versicherungen\" der Objektdetailseite hochladen.",
            }, ToolResultJsonOptions);
        }

        var absolutePdfPath = Path.Combine(_storageOptions.DataDirectoryAbsolute, policy.PdfFilePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(absolutePdfPath))
        {
            return JsonSerializer.Serialize(new { error = $"Die hinterlegte PDF-Datei für \"{property.Name}\" ({categoryLabel}) wurde auf dem Server nicht gefunden." }, ToolResultJsonOptions);
        }

        string policyText;
        await using (var stream = File.OpenRead(absolutePdfPath))
        {
            policyText = await _pdfTextExtractor.ExtractTextAsync(stream, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(policyText))
        {
            return JsonSerializer.Serialize(new { error = "Im PDF wurde kein durchsuchbarer Text gefunden (evtl. ein gescanntes Dokument ohne Text-Layer)." }, ToolResultJsonOptions);
        }

        var analysis = await _policyAnalysisService.AnalyzeAsync(policyText, category, cancellationToken);

        // AI-Werte bevorzugen, aber vorhandene manuelle Angaben nicht mit "nicht gefunden" (null)
        // überschreiben - ein Analyse-Lauf soll bestehende, per Hand gepflegte Daten nicht verschlechtern.
        var updatedPolicy = new InsurancePolicy
        {
            PropertyId = propertyId,
            Category = category,
            Provider = analysis.Provider ?? policy.Provider,
            PolicyNumber = analysis.PolicyNumber ?? policy.PolicyNumber,
            AnnualPremium = analysis.AnnualPremium ?? policy.AnnualPremium,
            StartDate = ParseLenientDate(analysis.StartDate) ?? policy.StartDate,
            ExpirationDate = ParseLenientDate(analysis.ExpirationDate) ?? policy.ExpirationDate,
        };
        await _insuranceService.UpsertPolicyAsync(updatedPolicy);

        var checkItems = (await _insuranceService.GetCheckItemsAsync(propertyId))
            .Where(c => c.Category == category)
            .ToDictionary(c => c.Key);

        var gaps = new List<string>();
        foreach (var finding in analysis.CheckFindings)
        {
            if (!checkItems.TryGetValue(finding.Key, out var item))
            {
                continue;
            }

            item.IsCovered = finding.Covered ?? item.IsCovered;
            item.Notes = string.IsNullOrWhiteSpace(finding.Note) ? item.Notes : finding.Note;
            await _insuranceService.UpdateCheckItemAsync(item);

            if (finding.Covered == false)
            {
                gaps.Add(item.Title);
            }
        }

        var benchmark = _insuranceService.CalculateBenchmark(property, category, updatedPolicy.AnnualPremium);
        var benchmarkLabel = benchmark.Status switch
        {
            InsuranceBenchmarkStatus.ImRahmen => "Grün (im Rahmen)",
            InsuranceBenchmarkStatus.UngewoehnlichGuenstig => "Rot (ungewöhnlich günstig - Gefahr von Deckungslücken)",
            InsuranceBenchmarkStatus.ZuTeuer => "Gelb (über dem Richtwert)",
            _ => "nicht ermittelbar (keine Einheiten oder kein Jahresbeitrag hinterlegt)",
        };

        // Der Rohtext (gekürzt) geht mit zurück, damit Armin auch abweichende Detailfragen zur Police
        // (z. B. Selbstbeteiligung, genaue Ausschlüsse) im selben oder einem späteren Gespräch
        // beantworten kann, ohne dass dafür jeder denkbare Punkt im festen Schema stehen müsste.
        return JsonSerializer.Serialize(new
        {
            objekt = property.Name,
            kategorie = categoryLabel,
            anbieter = updatedPolicy.Provider,
            scheinnummer = updatedPolicy.PolicyNumber,
            jahresbeitrag = updatedPolicy.AnnualPremium,
            kostenProEinheitProJahr = benchmark.Status == InsuranceBenchmarkStatus.NichtErmittelbar ? null : (decimal?)benchmark.CostPerUnitAnnual,
            kostenProQmProJahr = benchmark.Status == InsuranceBenchmarkStatus.NichtErmittelbar ? null : (decimal?)benchmark.CostPerSqmAnnual,
            benchmark = benchmarkLabel,
            gefundeneLuecken = gaps,
            zusammenfassungDerKi = analysis.Summary,
            auszugAusDemPolicentext = policyText.Length > 4000 ? policyText[..4000] : policyText,
        }, ToolResultJsonOptions);
    }

    private static DateOnly? ParseLenientDate(string? text) =>
        !string.IsNullOrWhiteSpace(text) && DateOnly.TryParse(text, De, System.Globalization.DateTimeStyles.None, out var date) ? date : null;

    /// <summary>Liest alle für Immobilie(+Einheit)+Jahr hinterlegten Nebenkostenabrechnungs-PDFs vom
    /// Datenträger (jedes Mal frisch) und lässt jede einzeln per Claude analysieren. Bewusst mehrere
    /// Dokumente statt eines einzelnen - manche Hausverwaltungen stellen je Einheit eine eigene,
    /// personalisierte Abrechnung aus statt einer gemeinsamen Abrechnung fürs ganze Objekt. Die je
    /// Dokument erkannten Kostenpositionen werden zusammengeführt, die je Dokument erkannten
    /// Gesamtkosten aufsummiert - das setzt voraus, dass die Dokumente sich nicht überschneiden (z. B.
    /// nicht versehentlich dieselbe Abrechnung zweimal hochgeladen wurde). "unitId" ist optional -
    /// weggelassen zielt das Tool auf die Ganzes-Objekt-Abrechnung (wie bisher); gesetzt auf die
    /// Abrechnung dieser einzelnen Einheit. Die tatsächlichen PDF-Pfade werden serverseitig aus der
    /// Datenbank aufgelöst, das von der KI in den Dokumenten erkannte Jahr wird nur informativ
    /// zurückgegeben, aber nicht für die Datenbank-Zuordnung verwendet (sonst könnte ein
    /// KI-Lesefehler versehentlich die falsche Jahres-Abrechnung überschreiben).</summary>
    private async Task<string> AnalyzeUtilityStatementPdfAsync(int propertyId, int? propertyUnitId, int year, CancellationToken cancellationToken)
    {
        var property = await _propertyService.GetByIdAsync(propertyId);
        if (property is null)
        {
            return JsonSerializer.Serialize(new { error = $"Keine Immobilie mit Id {propertyId} gefunden." }, ToolResultJsonOptions);
        }

        PropertyUnit? unit = null;
        if (propertyUnitId is not null)
        {
            unit = property.Units.FirstOrDefault(u => u.Id == propertyUnitId);
            if (unit is null)
            {
                return JsonSerializer.Serialize(new { error = $"Keine Einheit mit Id {propertyUnitId} bei \"{property.Name}\" gefunden." }, ToolResultJsonOptions);
            }
        }

        var scopeLabel = unit is null ? $"\"{property.Name}\"" : $"\"{unit.Label}\" bei \"{property.Name}\"";
        var statement = await _utilityService.GetStatementAsync(propertyId, propertyUnitId, year);
        if (statement is null || statement.Documents.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"Für {scopeLabel} wurde noch keine Nebenkostenabrechnung {year} als PDF hochgeladen. " +
                    "Bitte zuerst im Tab \"Nebenkosten\" der Objektdetailseite bzw. auf der Einheiten-Detailseite hochladen.",
            }, ToolResultJsonOptions);
        }

        var documentResults = new List<object>();
        var mergedCostItems = new List<(UtilityCostCategory Category, string Description, decimal Amount)>();
        decimal? mergedTotalCosts = null;
        int? detectedYear = null;

        foreach (var document in statement.Documents)
        {
            var absolutePdfPath = Path.Combine(_storageOptions.DataDirectoryAbsolute, document.FilePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePdfPath))
            {
                documentResults.Add(new { datei = document.FileName, fehler = "Datei wurde auf dem Server nicht gefunden." });
                continue;
            }

            string documentText;
            await using (var stream = File.OpenRead(absolutePdfPath))
            {
                documentText = await _pdfTextExtractor.ExtractTextAsync(stream, cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(documentText))
            {
                documentResults.Add(new { datei = document.FileName, fehler = "Kein durchsuchbarer Text gefunden (evtl. gescanntes Dokument ohne Text-Layer)." });
                continue;
            }

            var analysis = await _utilityAnalysisService.AnalyzeAsync(documentText, cancellationToken);
            detectedYear ??= analysis.Year;

            if (analysis.TotalCosts is not null)
            {
                mergedTotalCosts = (mergedTotalCosts ?? 0) + analysis.TotalCosts.Value;
            }

            foreach (var finding in analysis.CostItems)
            {
                if (!Enum.TryParse<UtilityCostCategory>(finding.Category, out var category))
                {
                    category = UtilityCostCategory.SonstigeBetrKV;
                }

                mergedCostItems.Add((category, finding.Description, finding.Amount));
            }

            documentResults.Add(new
            {
                datei = document.FileName,
                gesamtkostenInDiesemDokument = analysis.TotalCosts,
                positionenInDiesemDokument = analysis.CostItems.Count,
                zusammenfassung = analysis.Summary,
            });
        }

        var savedStatement = await _utilityService.UpsertStatementAsync(new UtilityStatement
        {
            PropertyId = propertyId,
            PropertyUnitId = propertyUnitId,
            Year = year,
            TotalCosts = mergedTotalCosts ?? statement.TotalCosts,
            IsCompleted = statement.IsCompleted,
        });

        // Bei erneuter Analyse werden die zuvor extrahierten Positionen ersetzt statt dupliziert -
        // anders als bei der festen Versicherungs-Checkliste gibt es hier keinen stabilen Schlüssel,
        // über den sich einzelne Positionen zuverlässig wiedererkennen und gezielt aktualisieren ließen.
        foreach (var existingItem in statement.Items)
        {
            await _utilityService.DeleteItemAsync(existingItem.Id);
        }

        var savedItems = new List<UtilityCostItem>();
        foreach (var (category, description, amount) in mergedCostItems)
        {
            savedItems.Add(await _utilityService.CreateItemAsync(new UtilityCostItem
            {
                UtilityStatementId = savedStatement.Id,
                Category = category,
                Description = description,
                Amount = amount,
            }));
        }

        var areaSqm = unit?.AreaSqm ?? property.LivingAreaSqm;
        var unitCountForKpi = unit is null ? property.Units.Count : 1;
        var kpi = _utilityService.CalculateKpi(year, savedStatement.TotalCosts, areaSqm, unitCountForKpi);
        var largestFactor = savedItems
            .GroupBy(i => i.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(i => i.Amount) })
            .OrderByDescending(g => g.Total)
            .FirstOrDefault();

        return JsonSerializer.Serialize(new
        {
            objekt = property.Name,
            einheit = unit?.Label,
            jahr = year,
            erkanntesJahrInDenDokumenten = detectedYear,
            anzahlAusgewerteterDokumente = statement.Documents.Count,
            dokumente = documentResults,
            gesamtkosten = savedStatement.TotalCosts,
            kostenProEinheitProJahr = kpi.CostPerUnitAnnual,
            kostenProQmProJahr = kpi.CostPerSqmAnnual,
            kostenProQmProMonat = kpi.CostPerSqmMonthly,
            groessterKostenfaktor = largestFactor is null ? null : new
            {
                kategorie = UtilityCostCatalog.CategoryDisplayNames[largestFactor.Category],
                betrag = largestFactor.Total,
            },
            positionenGespeichert = savedItems.Count,
        }, ToolResultJsonOptions);
    }

    /// <summary>Liest den für eine Einheit hinterlegten Mietvertrag ein und legt/aktualisiert das
    /// zugehörige Mietverhältnis. Nimmt bewusst propertyId+unitId statt einer rohen tenancyId als
    /// Tool-Eingabe entgegen (Armin kennt unitId aus get_property_details) und wählt serverseitig das
    /// jüngste Mietverhältnis mit hinterlegter PDF dieser Einheit - i. d. R. der zuletzt hochgeladene,
    /// noch auszuwertende Vertrag.</summary>
    private async Task<string> AnalyzeLeasePdfAsync(int propertyId, int unitId, CancellationToken cancellationToken)
    {
        var property = await _propertyService.GetByIdAsync(propertyId);
        if (property is null)
        {
            return JsonSerializer.Serialize(new { error = $"Keine Immobilie mit Id {propertyId} gefunden." }, ToolResultJsonOptions);
        }

        var unit = property.Units.FirstOrDefault(u => u.Id == unitId);
        if (unit is null)
        {
            return JsonSerializer.Serialize(new { error = $"Einheit mit Id {unitId} wurde bei \"{property.Name}\" nicht gefunden." }, ToolResultJsonOptions);
        }

        var tenancy = unit.Tenancies
            .Where(t => !string.IsNullOrWhiteSpace(t.PdfFilePath))
            .OrderByDescending(t => t.Id)
            .FirstOrDefault();

        if (tenancy is null)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"Für \"{unit.Label}\" bei \"{property.Name}\" wurde noch kein Mietvertrag als PDF hochgeladen. " +
                    "Bitte zuerst auf der Einheiten-Detailseite hochladen.",
            }, ToolResultJsonOptions);
        }

        var absolutePdfPath = Path.Combine(_storageOptions.DataDirectoryAbsolute, tenancy.PdfFilePath!.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(absolutePdfPath))
        {
            return JsonSerializer.Serialize(new { error = $"Die hinterlegte Mietvertrags-PDF für \"{unit.Label}\" wurde auf dem Server nicht gefunden." }, ToolResultJsonOptions);
        }

        string leaseText;
        await using (var stream = File.OpenRead(absolutePdfPath))
        {
            leaseText = await _pdfTextExtractor.ExtractTextAsync(stream, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(leaseText))
        {
            return JsonSerializer.Serialize(new { error = "Im PDF wurde kein durchsuchbarer Text gefunden (evtl. ein gescanntes Dokument ohne Text-Layer)." }, ToolResultJsonOptions);
        }

        var analysis = await _leaseAnalysisService.AnalyzeAsync(leaseText, cancellationToken);

        var updated = new Tenancy
        {
            Id = tenancy.Id,
            PropertyUnitId = unitId,
            TenantName = !string.IsNullOrWhiteSpace(analysis.TenantName) ? analysis.TenantName! : tenancy.TenantName,
            TenantEmail = analysis.TenantEmail ?? tenancy.TenantEmail,
            TenantPhone = analysis.TenantPhone ?? tenancy.TenantPhone,
            MoveInDate = ParseLenientDate(analysis.MoveInDate) ?? tenancy.MoveInDate,
            MoveOutDate = ParseLenientDate(analysis.MoveOutDate) ?? tenancy.MoveOutDate,
            ColdRentMonthly = analysis.ColdRentMonthly ?? tenancy.ColdRentMonthly,
            AdvancePaymentMonthly = analysis.AdvancePaymentMonthly ?? tenancy.AdvancePaymentMonthly,
            SecurityDeposit = analysis.SecurityDeposit ?? tenancy.SecurityDeposit,
            PdfFilePath = tenancy.PdfFilePath,
            PdfFileName = tenancy.PdfFileName,
            Notes = tenancy.Notes,
        };

        await _tenancyService.UpdateAsync(updated);

        // Der Rohtext (gekürzt) geht mit zurück, damit Armin auch abweichende Detailfragen zum
        // Vertragstext im selben oder einem späteren Gespräch beantworten kann.
        return JsonSerializer.Serialize(new
        {
            objekt = property.Name,
            einheit = unit.Label,
            mieter = updated.TenantName,
            email = updated.TenantEmail,
            telefon = updated.TenantPhone,
            mietbeginn = updated.MoveInDate,
            mietende = updated.MoveOutDate,
            kaltmieteProMonat = updated.ColdRentMonthly,
            nebenkostenvorauszahlungProMonat = updated.AdvancePaymentMonthly,
            kaution = updated.SecurityDeposit,
            zusammenfassungDerKi = analysis.Summary,
            auszugAusDemMietvertrag = leaseText.Length > 4000 ? leaseText[..4000] : leaseText,
        }, ToolResultJsonOptions);
    }

    private async Task<(string FileName, string Url)> GenerateTenantLetterAsync(IReadOnlyDictionary<string, JsonElement> input, CancellationToken cancellationToken)
    {
        var letterTypeRaw = GetRequiredString(input, "letterType");
        if (!Enum.TryParse<TenantLetterType>(letterTypeRaw, out var letterType))
        {
            throw new InvalidOperationException($"Unbekannter letterType: {letterTypeRaw}");
        }

        return await _letterGenerator.GenerateAsync(
            GetPropertyId(input),
            GetUnitId(input),
            letterType,
            GetRequiredString(input, "subject"),
            GetRequiredString(input, "bodyText"),
            GetOptionalString(input, "senderName"),
            GetOptionalString(input, "senderAddress"),
            cancellationToken);
    }

    private static List<ToolUnion> BuildTools() => new()
    {
        new Tool
        {
            Name = "get_portfolio_summary",
            Description = "Liefert eine Zusammenfassung des gesamten Immobilienportfolios: Gesamtmieten, " +
                "Objektanzahl, Gesamtwert und Gesamtrestschuld (anteilig nach Beteiligungsquote). Nutze " +
                "dieses Tool bei allgemeinen Fragen zum Gesamtportfolio.",
            InputSchema = new() { Properties = new Dictionary<string, JsonElement>(), Required = Array.Empty<string>() },
        },
        new Tool
        {
            Name = "get_property_details",
            Description = "Liefert alle Stammdaten, Finanzierungen, Renovierungen, Kennzahlen und Bildpfade " +
                "einer bestimmten Immobilie anhand ihrer Id.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["propertyId"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Die Id der Immobilie." }),
                },
                Required = ["propertyId"],
            },
        },
        new Tool
        {
            Name = "query_database_custom",
            Description = "Liefert einen kompakten Überblick über alle Immobilien und Ankaufsprüfungen im " +
                "Portfolio (Ids, Namen, Kaufpreise, Marktwerte, Restschulden). Nutze dieses Tool, um die Id " +
                "einer Immobilie anhand ihres Namens zu ermitteln, oder für Fragen, die sich nicht mit den " +
                "anderen Tools beantworten lassen.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["queryIntent"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Kurze Beschreibung, wonach gesucht wird." }),
                },
                Required = ["queryIntent"],
            },
        },
        new Tool
        {
            Name = "generate_expose_pdf",
            Description = "Erstellt ein professionelles PDF-Exposé (Fotos, Objektdaten, Beschreibung, " +
                "Kennzahlen-Übersicht) für eine Immobilie und speichert es zum Download.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["propertyId"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Die Id der Immobilie." }),
                },
                Required = ["propertyId"],
            },
        },
        new Tool
        {
            Name = "generate_power_point",
            Description = "Erstellt eine rudimentäre PowerPoint-Präsentation (Titel, Objektdaten/Kennzahlen, " +
                "Finanzierung) für ein Banktermin-Gespräch zu einer Immobilie und speichert sie zum Download.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["propertyId"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Die Id der Immobilie." }),
                },
                Required = ["propertyId"],
            },
        },
        new Tool
        {
            Name = "analyze_insurance_policy_pdf",
            Description = "Liest die für eine Immobilie im Tab \"Versicherungen\" hochgeladene Policen-PDF " +
                "(Gebäudeversicherung oder Haus-/Grundbesitzerhaftpflicht) ein, extrahiert Versicherungsgesellschaft, " +
                "Scheinnummer, Jahresbeitrag und Ablaufdatum, gleicht die Vertragsbedingungen mit der Prüf-Checkliste " +
                "ab (z. B. Elementarschutz, grobe Fahrlässigkeit, Deckungssumme) und speichert die Ergebnisse. " +
                "Nutze dieses Tool jedes Mal neu, wenn nach Details zu einer konkreten Police gefragt wird (auch für " +
                "Fragen zum genauen Vertragstext) - es liest die hinterlegte PDF jedes Mal frisch ein, ein erneuter " +
                "Upload ist nicht nötig. Falls noch keine PDF hochgeladen wurde, liefert das Tool einen entsprechenden Hinweis.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["propertyId"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Die Id der Immobilie." }),
                    ["insuranceCategory"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "string",
                        @enum = new[] { "Gebaeudeversicherung", "HausUndGrundbesitzerhaftpflicht" },
                        description = "Welche Versicherungskategorie analysiert werden soll.",
                    }),
                },
                Required = ["propertyId", "insuranceCategory"],
            },
        },
        new Tool
        {
            Name = "analyze_utility_statement_pdf",
            Description = "Liest alle für eine Immobilie (ohne unitId) oder für eine einzelne Einheit (mit unitId) " +
                "für ein Abrechnungsjahr hochgeladenen Nebenkosten-/Betriebskostenabrechnungs-PDFs ein (es können " +
                "mehrere sein, z. B. mehrere Seiten derselben Abrechnung), extrahiert je Dokument die Gesamtsumme " +
                "sowie alle einzelnen Kostenpositionen gemäß Betriebskostenverordnung (Heizung/Warmwasser, " +
                "Grundsteuer, Müll, Wasser, Hausstrom etc.), führt die Ergebnisse aller Dokumente zusammen " +
                "(Kostenpositionen kombiniert, Gesamtsummen aufaddiert) und speichert die zusammengeführte " +
                "Abrechnung samt Positionen. Die App unterscheidet Ganzes-Objekt-Abrechnungen (keine unitId - " +
                "z. B. bei Selbstverwaltung oder wenn die Hausverwaltung nicht personalisiert abrechnet) von " +
                "Einheiten-Abrechnungen (mit unitId - die häufigere Praxis bei personalisierter Abrechnung je " +
                "Wohnung); beide können nebeneinander existieren, das Objekt-Gesamt ist einfach die Summe. Löst " +
                "dabei die Dashboard-Erinnerung für fehlende Abrechnungen dieses Jahres auf. Nutze dieses Tool " +
                "jedes Mal neu bei Fragen zu einer konkreten Abrechnung - es liest die hinterlegten PDFs jedes " +
                "Mal frisch ein. Falls noch keine PDF hochgeladen wurde, liefert das Tool einen entsprechenden " +
                "Hinweis.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["propertyId"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Die Id der Immobilie." }),
                    ["unitId"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "integer",
                        description = "Optional: Die Id der Einheit (aus get_property_details, Feld \"einheiten[].unitId\"), " +
                            "falls die Abrechnung für eine einzelne Einheit statt fürs ganze Objekt gilt.",
                    }),
                    ["year"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Das Abrechnungsjahr, z. B. 2024." }),
                },
                Required = ["propertyId", "year"],
            },
        },
        new Tool
        {
            Name = "analyze_lease_pdf",
            Description = "Liest den für eine Einheit auf der Einheiten-Detailseite hochgeladenen Mietvertrag ein, " +
                "extrahiert Mieter (Name, E-Mail, Telefon), Mietbeginn/-ende, Kaltmiete, Nebenkostenvorauszahlung " +
                "und Kaution, und legt/aktualisiert damit automatisch das Mietverhältnis der Einheit. Nutze dieses " +
                "Tool jedes Mal neu bei Fragen zu einem konkreten Mietvertrag (auch für Detailfragen zum Vertragstext, " +
                "z. B. Kündigungsfristen) - es liest die hinterlegte PDF jedes Mal frisch ein. Falls noch kein " +
                "Mietvertrag hochgeladen wurde, liefert das Tool einen entsprechenden Hinweis.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["propertyId"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Die Id der Immobilie." }),
                    ["unitId"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Die Id der Einheit (aus get_property_details, Feld \"einheiten[].unitId\")." }),
                },
                Required = ["propertyId", "unitId"],
            },
        },
        new Tool
        {
            Name = "generate_tenant_letter",
            Description = "Erstellt einen PDF-Entwurf für ein Schreiben an den aktuellen Mieter einer Einheit " +
                "(Mahnung, einfaches Anschreiben oder Kündigung). Du formulierst \"subject\" und \"bodyText\" selbst " +
                "passend zum konkreten Anlass aus dem Gespräch (z. B. offener Betrag und Frist bei einer Mahnung, " +
                "Kündigungsgrund und -datum bei einer Kündigung) - das Tool übernimmt nur Absender-/Empfänger-/" +
                "Objektbezug und die Formatierung als Brief. Erfordert ein aktuelles Mietverhältnis für die Einheit. " +
                "WICHTIG: Erzeugt nur einen Entwurf zum Download - versendet nichts selbst. Weise den Nutzer darauf " +
                "hin, das Schreiben vor Versand zu prüfen (bei Kündigungen insbesondere rechtlich, da im deutschen " +
                "Mietrecht strenge Fristen und Formvorschriften gelten).",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["propertyId"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Die Id der Immobilie." }),
                    ["unitId"] = JsonSerializer.SerializeToElement(new { type = "integer", description = "Die Id der Einheit (aus get_property_details, Feld \"einheiten[].unitId\")." }),
                    ["letterType"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "string",
                        @enum = new[] { "Mahnung", "Anschreiben", "Kuendigung" },
                        description = "Art des Schreibens.",
                    }),
                    ["subject"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Betreffzeile des Briefs." }),
                    ["bodyText"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Der vollständige, von dir formulierte Brieftext (ohne Anrede/Grußformel, die fügt das Tool selbst hinzu)." }),
                    ["senderName"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Name des Absenders/Vermieters, falls im Gespräch bekannt." }),
                    ["senderAddress"] = JsonSerializer.SerializeToElement(new { type = "string", description = "Absenderadresse, falls im Gespräch bekannt." }),
                },
                Required = ["propertyId", "unitId", "letterType", "subject", "bodyText"],
            },
        },
    };
}
