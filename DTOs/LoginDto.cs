namespace Prismon.Api.DTOs;

public class LoginEmailRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginWalletRequest
{
    public string WalletPublicKey { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public Guid ChallengeId { get; set; } 
}

public class LoginResponse
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserWallet { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}