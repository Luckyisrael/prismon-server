using Prismon.Api.Models;

namespace Prismon.Api.Interface;

public interface IUserOnboardingService
{
    Task<UserOnboardingResponse> ConnectWalletAsync(App app, string walletPublicKey, string signature);
    Task<UserOnboardingResponse> RegisterEmailAsync(App app, string email, string password);
    Task<UserOnboardingResponse> VerifyEmailAsync(App app, string email, string verificationCode);
}

public class UserOnboardingResponse
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string? VerificationCode { get; set; } // For email registration
}