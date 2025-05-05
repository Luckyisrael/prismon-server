namespace Prismon.Api.DTOs;
public class OrganizationResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid OwnerId { get; set; } 
    public DateTime CreatedAt { get; set; }
}