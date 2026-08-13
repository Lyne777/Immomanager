using Immomanager.Web.Data;
using Immomanager.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Immomanager.Web.Services;

public class PropertyService : IPropertyService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly StorageOptions _storageOptions;

    public PropertyService(IDbContextFactory<ApplicationDbContext> contextFactory, StorageOptions storageOptions)
    {
        _contextFactory = contextFactory;
        _storageOptions = storageOptions;
    }

    public async Task<List<Property>> GetAllAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Properties
            .Include(p => p.Financings)
            .Include(p => p.RentTargets)
            .Include(p => p.Units).ThenInclude(u => u.Tenancies)
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<Property?> GetByIdAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Properties
            .Include(p => p.Financings)
            .Include(p => p.RentTargets)
            .Include(p => p.Units).ThenInclude(u => u.Tenancies)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Property> CreateAsync(Property property)
    {
        // Jede Immobilie braucht mindestens eine Einheit, sonst zeigen Fläche/Miete/Kosten (jetzt aus
        // Units berechnet) direkt nach dem Anlegen überall 0 - die Einheit wird danach im "Einheiten"-
        // Tab der Objektdetailseite mit echten Werten befüllt.
        if (property.Units.Count == 0)
        {
            property.Units.Add(new PropertyUnit { Label = "Einheit 1" });
        }

        await using var db = await _contextFactory.CreateDbContextAsync();
        db.Properties.Add(property);
        await db.SaveChangesAsync();
        return property;
    }

    public async Task UpdateAsync(Property property)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        // Bewusst nur die Property selbst als geändert markieren, nicht den ganzen Objektgraphen:
        // "property" kommt aus dem Bearbeiten-Formular und trägt wegen des Include() beim Laden
        // weiterhin die vollständigen Financings/RentTargets-Listen. db.Properties.Update(property)
        // würde diese Kind-Entitäten ebenfalls als Modified markieren und bei jedem Stammdaten-Save
        // unnötig neu schreiben - im schlimmsten Fall verändert das gleichzeitig laufende Bearbeitung
        // von Darlehen/Soll-Werten (in einem anderen Tab) durch den veralteten, mitgeladenen Stand.
        db.Attach(property);
        db.Entry(property).State = EntityState.Modified;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var property = await db.Properties.FindAsync(id);
        if (property is not null)
        {
            db.Properties.Remove(property);
            await db.SaveChangesAsync();
        }

        // Bilddateien liegen außerhalb der DB (Cascade-Delete entfernt nur die Datensätze),
        // daher hier zusätzlich den Upload-Ordner der Immobilie vom Dateisystem entfernen.
        var propertyUploadsDirectory = Path.Combine(_storageOptions.UploadsDirectoryAbsolute, id.ToString());
        if (Directory.Exists(propertyUploadsDirectory))
        {
            Directory.Delete(propertyUploadsDirectory, recursive: true);
        }
    }
}
