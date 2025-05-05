namespace Prismon.Api.Models;

public class App
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string? DeveloperId { get; set; } // string, not Guid
    public ApplicationUser? Developer { get; set; }
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ProgramId { get; set; }
    public string? DeployedEndpoint { get; set; }
    public DateTime? DeployedAt { get; set; }
    public string Tier { get; set; } = "Free"; // Free, Premium, Enterprise
    public int? CustomRateLimit { get; set; } // For Enterprise
}