using Microsoft.EntityFrameworkCore;
using Prismon.Api.Data;
using Prismon.Api.DTOs;
using Prismon.Api.Models;
using BCrypt.Net;
using Prismon.Api.Interface;

namespace Prismon.Api.Services;

public class UserProfileService : IUserProfileService
{
    private readonly PrismonDbContext _dbContext;
    private readonly ILogger<UserProfileService> _logger;

    public UserProfileService(PrismonDbContext dbContext, ILogger<UserProfileService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ProfileResponse> UpdateProfileAsync(string userId, Guid appId, UpdateProfileRequest request)
    {
        var user = await _dbContext.DAppUsers
            .FirstOrDefaultAsync(u => u.Id.ToString() == userId && u.AppId == appId);
        if (user == null)
        {
            return new ProfileResponse { Succeeded = false, Message = "User not found" };
        }

        if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
        {
            if (await _dbContext.DAppUsers.AnyAsync(u => u.AppId == appId && u.Email == request.Email))
            {
                return new ProfileResponse { Succeeded = false, Message = "Email already in use" };
            }
            user.Email = request.Email;
            user.IsEmailVerified = false; // Require re-verification
            user.VerificationCode = Guid.NewGuid().ToString("N")[..6].ToUpper();
            user.CodeExpiresAt = DateTime.UtcNow.AddHours(1);
        }

        if (!string.IsNullOrEmpty(request.Password))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Profile updated for user {UserId} in app {AppId}", userId, appId);
        return MapToResponse(user, true, "Profile updated successfully");
    }

    public async Task<ProfileResponse> DisconnectWalletAsync(string userId, Guid appId)
    {
        var user = await _dbContext.DAppUsers
            .FirstOrDefaultAsync(u => u.Id.ToString() == userId && u.AppId == appId);
        if (user == null)
        {
            return new ProfileResponse { Succeeded = false, Message = "User not found" };
        }

        if (string.IsNullOrEmpty(user.WalletPublicKey))
        {
            return new ProfileResponse { Succeeded = false, Message = "No wallet connected" };
        }

        user.WalletPublicKey = null;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Wallet disconnected for user {UserId} in app {AppId}", userId, appId);
        return MapToResponse(user, true, "Wallet disconnected successfully");
    }

    private static ProfileResponse MapToResponse(DAppUser user, bool succeeded, string message)
    {
        return new ProfileResponse
        {
            Succeeded = succeeded,
            Message = message,
            UserId = user.Id.ToString(),
            Email = user.Email,
            IsEmailVerified = user.IsEmailVerified,
            WalletPublicKey = user.WalletPublicKey
        };
    }
}