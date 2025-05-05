using Prismon.Api.DTOs;
using Prismon.Api.Models;

namespace Prismon.Api.Interface;

public interface IAppService
{
    Task<AppResponse> CreateAppAsync(CreateAppRequest request, string developerId, Guid? organizationId = null);
    Task<List<AppResponse>> GetAppsAsync(string developerId);
    Task<AppResponse?> GetAppAsync(Guid appId, string developerId);

     Task<OrganizationResponse> CreateOrganizationAsync(CreateOrganizationRequest request, string developerId);
    Task<bool> CompleteOnboardingAsync(string developerId);
    Task<AppResponse> UpdateAppAsync(Guid appId, UpdateAppRequest request, string developerId);
    Task<AppResponse> DeleteAppAsync(Guid appId, string developerId);
    Task<AppResponse> RegenerateApiKeyAsync(Guid appId, string developerId);
}
