namespace Prismon.Api.Models;

public class LoginChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AppId { get; set; }
    public string WalletPublicKey { get; set; } = string.Empty;
    public string Challenge { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(5); // 5-minute expiration
}