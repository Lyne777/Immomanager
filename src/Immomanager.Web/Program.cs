using System.Globalization;
using Immomanager.Web.Components;
using Immomanager.Web.Data;
using Immomanager.Web.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using MudBlazor.Services;

var germanCulture = new CultureInfo("de-DE");
CultureInfo.DefaultThreadCurrentCulture = germanCulture;
CultureInfo.DefaultThreadCurrentUICulture = germanCulture;

// QuestPDF Community-Lizenz: kostenlos für Privatpersonen/Unternehmen mit < 1 Mio. USD Jahresumsatz.
// Bei Überschreiten ist eine kommerzielle Lizenz erforderlich - siehe https://www.questpdf.com/pricing.html.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Fotos werden über die Blazor-Server-SignalR-Verbindung hochgeladen; die Standard-Nachrichtengröße
// (32 KB) reicht dafür nicht aus, daher hier an die Bild-Obergrenze (PropertyImageService) angleichen.
builder.Services.Configure<HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = PropertyImageService.MaxFileSizeBytes + 1024 * 64;
});

// SQLite-Datenbankdatei liegt in einem konfigurierbaren Ordner (Standard: ./data),
// damit dieser in Docker/Synology als persistentes Volume gemountet werden kann.
var dataDirectory = builder.Configuration["Storage:DataDirectory"] ?? "./data";
if (!Path.IsPathRooted(dataDirectory))
{
    dataDirectory = Path.Combine(builder.Environment.ContentRootPath, dataDirectory);
}
Directory.CreateDirectory(dataDirectory);

var databaseFileName = builder.Configuration["Storage:DatabaseFileName"] ?? "app.db";
var databasePath = Path.Combine(dataDirectory, databaseFileName);
var connectionString = $"Data Source={databasePath}";

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// Fotos werden unterhalb des Datenverzeichnisses in "uploads/" abgelegt (in Docker/Synology also
// unter dem gemounteten Volume) - die DB speichert dafür nur den relativen Pfad ab.
var uploadsDirectory = Path.Combine(dataDirectory, StorageOptions.UploadsRelativeRoot);
Directory.CreateDirectory(uploadsDirectory);

// Von Armin Asset generierte Exposé-PDFs/Präsentationen landen in "exports/" - ebenfalls unterhalb
// des Datenverzeichnisses und damit automatisch über die "/data-files"-Middleware herunterladbar.
var exportsDirectory = Path.Combine(dataDirectory, StorageOptions.ExportsRelativeRoot);
Directory.CreateDirectory(exportsDirectory);

// Datenbank-Sicherungen landen in "backups/" - ebenfalls unterhalb des Datenverzeichnisses, damit
// sie das gemountete Docker/Synology-Volume mit sichert und über "/data-files" herunterladbar sind.
var backupsDirectory = Path.Combine(dataDirectory, StorageOptions.BackupsRelativeRoot);
Directory.CreateDirectory(backupsDirectory);

// Hochgeladene Versicherungspolicen (PDFs) landen in "policies/" - ebenfalls unterhalb des
// Datenverzeichnisses, damit Armin Asset jederzeit erneut darauf zugreifen kann.
var policiesDirectory = Path.Combine(dataDirectory, StorageOptions.PoliciesRelativeRoot);
Directory.CreateDirectory(policiesDirectory);

// Hochgeladene Nebenkostenabrechnungen (PDFs) landen in "utility_statements/" - ebenfalls
// unterhalb des Datenverzeichnisses, damit Armin Asset jederzeit erneut darauf zugreifen kann.
var utilityStatementsDirectory = Path.Combine(dataDirectory, StorageOptions.UtilityStatementsRelativeRoot);
Directory.CreateDirectory(utilityStatementsDirectory);

// Hochgeladene Mietverträge (PDFs) landen in "leases/" - ebenfalls unterhalb des
// Datenverzeichnisses, damit Armin Asset jederzeit erneut darauf zugreifen kann.
var leasesDirectory = Path.Combine(dataDirectory, StorageOptions.LeasesRelativeRoot);
Directory.CreateDirectory(leasesDirectory);

// Allgemeine Objekt-/Einheiten-Dokumente (Energieausweis, Grundriss, ...) landen in "documents/" -
// ebenfalls unterhalb des Datenverzeichnisses.
var documentsDirectory = Path.Combine(dataDirectory, StorageOptions.DocumentsRelativeRoot);
Directory.CreateDirectory(documentsDirectory);

builder.Services.AddSingleton(new StorageOptions
{
    DataDirectoryAbsolute = dataDirectory,
    UploadsDirectoryAbsolute = uploadsDirectory,
    ExportsDirectoryAbsolute = exportsDirectory,
    BackupsDirectoryAbsolute = backupsDirectory,
    PoliciesDirectoryAbsolute = policiesDirectory,
    UtilityStatementsDirectoryAbsolute = utilityStatementsDirectory,
    LeasesDirectoryAbsolute = leasesDirectory,
    DocumentsDirectoryAbsolute = documentsDirectory,
    DatabaseFilePath = databasePath,
});

builder.Services.Configure<AnthropicOptions>(builder.Configuration.GetSection("Anthropic"));
builder.Services.AddScoped<IExposeParserService, ExposeParserService>();
builder.Services.AddScoped<IExposeAnalysisService, AnthropicExposeAnalysisService>();
builder.Services.AddScoped<IInsurancePolicyAnalysisService, AnthropicInsurancePolicyAnalysisService>();
builder.Services.AddScoped<IUtilityStatementAnalysisService, AnthropicUtilityStatementAnalysisService>();
builder.Services.AddScoped<ILeaseAnalysisService, AnthropicLeaseAnalysisService>();
builder.Services.AddScoped<IExposePdfGenerator, ExposePdfGenerator>();
builder.Services.AddScoped<IPropertyPowerPointGenerator, PropertyPowerPointGenerator>();
builder.Services.AddScoped<ITenantLetterPdfGenerator, TenantLetterPdfGenerator>();
builder.Services.AddScoped<IArminAssetAgentService, ArminAssetAgentService>();
builder.Services.AddScoped<IBackupService, BackupService>();

builder.Services.AddScoped<IPropertyService, PropertyService>();
builder.Services.AddScoped<IFinancingService, FinancingService>();
builder.Services.AddScoped<IPropertyUnitService, PropertyUnitService>();
builder.Services.AddScoped<IInsuranceService, InsuranceService>();
builder.Services.AddScoped<IUtilityService, UtilityService>();
builder.Services.AddScoped<ITenancyService, TenancyService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IDashboardTodoService, DashboardTodoService>();
builder.Services.AddScoped<IRentTargetService, RentTargetService>();
builder.Services.AddScoped<RentTargetAnalyticsService>();
builder.Services.AddScoped<IPropertyImageService, PropertyImageService>();
builder.Services.AddScoped<IRenovationService, RenovationService>();
builder.Services.AddScoped<RenovationAnalyticsService>();
builder.Services.AddScoped<IDealCalculationService, DealCalculationService>();
builder.Services.AddScoped<DealCalculationEngine>();
builder.Services.AddScoped<KpiCalculationService>();
builder.Services.AddScoped<ViewModeState>();

// Prüft periodisch gegen die öffentliche GitHub-API, ob auf main ein neuerer Commit als die
// laufende (ins Image gebackene) Version vorliegt - siehe VersionCheckBackgroundService.
builder.Services.AddHttpClient(nameof(VersionCheckBackgroundService), client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Immomanager-VersionCheck");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddSingleton<VersionCheckState>();
builder.Services.AddHostedService<VersionCheckBackgroundService>();

var app = builder.Build();

// Legt die SQLite-Datenbank samt Schema automatisch an, falls sie beim Start noch nicht existiert.
app.MigrateDatabase();

// Führt bei aktivierter Einstellung ein automatisches Backup des (gerade migrierten) Startzustands
// aus. Fehler hierbei dürfen den App-Start nicht verhindern, werden aber geloggt.
try
{
    await using var backupScope = app.Services.CreateAsyncScope();
    var backupService = backupScope.ServiceProvider.GetRequiredService<IBackupService>();
    await backupService.RunStartupAutoBackupIfEnabledAsync();
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Automatisches Start-Backup fehlgeschlagen.");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

// Serviert Dateien aus dem konfigurierbaren Datenverzeichnis (u. a. hochgeladene Fotos unter
// "uploads/") unter der URL "/data-files/...", da dieser Ordner außerhalb von wwwroot liegt.
// ".db" ist kein von ASP.NET Core bekannter Dateityp - ohne diese explizite Zuordnung würde die
// Middleware Backup-Downloads sonst stillschweigend überspringen (Ergebnis: 404).
var dataFilesContentTypeProvider = new FileExtensionContentTypeProvider();
dataFilesContentTypeProvider.Mappings[".db"] = "application/octet-stream";

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(dataDirectory),
    RequestPath = "/data-files",
    ContentTypeProvider = dataFilesContentTypeProvider,
});

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
