

using Microsoft.AspNetCore.Identity;

namespace Prismon.Api.Models;

public class ApplicationUser : IdentityUser
{
    public Guid DeveloperId { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? OrganizationId{ get; set; } // Nullable, links to team if joined
    public Organization? Organization { get; set; }
    public bool IsOnboardingComplete { get; set; } = false; // Tracks team/skip choice
    public string Tier { get; set; } = "Free"; // Free, Premium, Enterprise
    public int? CustomRateLimit { get; set; } // Monthly quota for Enterprise
    public List<App> Apps { get; set; } = new List<App>();
}