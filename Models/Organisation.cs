namespace Prismon.Api.Models;
public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty; // string, not Guid
    public ApplicationUser? Owner { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<App> Apps { get; set; } = new();
}