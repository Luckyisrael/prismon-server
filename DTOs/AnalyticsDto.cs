namespace Prismon.Api.DTOs;

public class UserActivityDto
{
    public Guid AppId { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveUsersLast24h { get; set; }
    public int RegistrationsLast7d { get; set; }
}

public class TransactionDto
{
    public string Signature { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty; // In SOL or lamports
    public DateTime? Timestamp { get; set; }
}