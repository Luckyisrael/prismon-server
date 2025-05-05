using Prismon.Api.DTOs;

namespace Prismon.Api.Interface;

public interface IUserProfileService
{
    Task<ProfileResponse> UpdateProfileAsync(string userId, Guid appId, UpdateProfileRequest request);
    Task<ProfileResponse> DisconnectWalletAsync(string userId, Guid appId);
}