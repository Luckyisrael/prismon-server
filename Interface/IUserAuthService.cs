using Prismon.Api.DTOs;

namespace Prismon.Api.Interface;

public interface IUserAuthService
{
    Task<LoginResponse> LoginWithEmailAsync(string email, string password, Guid appId);
    Task<LoginResponse> LoginWithWalletAsync(string walletPublicKey, string signature, Guid appId, Guid challengeId); // Add challengeId
}