namespace Prismon.Api.DTOs;

public class UpdateAppRequest
{
    public string Name { get; set; } = string.Empty;
}

public class AppResponse
{
    public bool? Succeeded { get; set; }
    public string? Message { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string? DeveloperId { get; set; }
    public Guid? OrganizationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ProgramId { get; set; }
    public string? DeployedEndpoint { get; set; }
    public DateTime? DeployedAt { get; set; }

}