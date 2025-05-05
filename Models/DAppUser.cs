using Microsoft.AspNetCore.Identity;

namespace Prismon.Api.Models;

public class DAppUser : IdentityUser<Guid> // Changed to inherit from IdentityUser<Guid>
{
    public string? WalletPublicKey { get; set; } // Optional for wallet users
    public bool IsEmailVerified { get; set; }
    public string? VerificationCode { get; set; }
    public DateTime? CodeExpiresAt { get; set; }
    public string? Email { get; set; }
    public string? PasswordHash { get; set; } 
    public Guid AppId { get; set; }
    public App App { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
}