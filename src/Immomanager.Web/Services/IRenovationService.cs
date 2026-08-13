using Immomanager.Web.Models;

namespace Immomanager.Web.Services;

public interface IRenovationService
{
    Task<List<RenovationProject>> GetProjectsByPropertyIdAsync(int propertyId);
    Task<RenovationProject?> GetProjectByIdAsync(int projectId);
    Task<List<RenovationProject>> GetAllProjectsAsync();

    Task<RenovationProject> CreateProjectAsync(RenovationProject project);
    Task UpdateProjectAsync(RenovationProject project);
    Task DeleteProjectAsync(int projectId);

    Task<RenovationLineItem> CreateLineItemAsync(RenovationLineItem lineItem);
    Task UpdateLineItemAsync(RenovationLineItem lineItem);
    Task DeleteLineItemAsync(int lineItemId);
}
