using Prismon.Api.Models;

namespace Prismon.Api.Services;

public interface IDeploymentService
{
    Task<DeploymentResponse> DeployDAppAsync(App app); // Mock deployment
    Task<DeploymentResponse> DeployDAppRealAsync(App app); // Real deployment
}

public class DeploymentResponse
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ProgramId { get; set; } = string.Empty;
    public string DeployedEndpoint { get; set; } = string.Empty;
}