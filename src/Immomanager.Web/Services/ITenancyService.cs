using Immomanager.Web.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace Immomanager.Web.Services;

public interface ITenancyService
{
    Task<List<Tenancy>> GetTenanciesAsync(int unitId);

    Task<Tenancy?> GetByIdAsync(int tenancyId);

    Task<Tenancy> CreateAsync(Tenancy tenancy);

    Task UpdateAsync(Tenancy tenancy);

    Task DeleteAsync(int tenancyId);

    /// <summary>Legt für die Einheit ein neues Mietverhältnis mit hochgeladenem Mietvertrag an
    /// (Platzhalter-Mieterdaten, zur weiteren Auswertung durch Armin Asset oder manuelle Eingabe).</summary>
    Task<Tenancy> UploadLeasePdfAsync(int unitId, IBrowserFile file, CancellationToken cancellationToken = default);
}
