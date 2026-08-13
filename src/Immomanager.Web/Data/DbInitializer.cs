using Microsoft.EntityFrameworkCore;

namespace Immomanager.Web.Data;

public static class DbInitializer
{
    /// <summary>Wendet beim ersten Start ausstehende Migrationen an, wodurch die SQLite-Datei
    /// (inkl. Schema) automatisch angelegt wird, falls sie noch nicht existiert.</summary>
    public static void MigrateDatabase(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();
    }
}
