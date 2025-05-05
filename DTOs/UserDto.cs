namespace Prismon.Api.DTOs;

public class UpdateProfileRequest
{
    public string? Email { get; set; }
    public string? Password { get; set; }
}

public class ProfileResponse
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsEmailVerified { get; set; }
    public string? WalletPublicKey { get; set; }
}