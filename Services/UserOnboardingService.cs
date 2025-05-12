using Microsoft.EntityFrameworkCore;
using Prismon.Api.Data;
using Prismon.Api.Models;
using System.Security.Cryptography;
using System.Text;
using Prismon.Api.Interface;
using Polly;
using Polly.Retry;

using Solnet.Wallet;

using Solnet.Wallet.Utilities;


namespace Prismon.Api.Services;

public class UserOnboardingService : IUserOnboardingService
{
    private readonly PrismonDbContext _dbContext;
    private readonly ILogger<UserOnboardingService> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public UserOnboardingService(PrismonDbContext dbContext, ILogger<UserOnboardingService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
        _retryPolicy = Policy
            .Handle<DbUpdateException>()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (exception, timespan, attempt, context) =>
                    _logger.LogWarning("Retry {Attempt} after {Seconds}s due to {Message}",
                        attempt, timespan.TotalSeconds, exception.Message));
    }

    public async Task<UserOnboardingResponse> RegisterEmailAsync(App app, string email, string password)
    {
        try
        {
            var existingUser = await _dbContext.DAppUsers
                .FirstOrDefaultAsync(u => u.AppId == app.Id && u.Email == email);

            if (existingUser != null)
            {
                return new UserOnboardingResponse
                {
                    Succeeded = false,
                    Message = "Email already registered for this app"
                };
            }

            var verificationCode = GenerateVerificationCode();
            var user = new DAppUser
            {
                Email = email,
                AppId = app.Id,
                IsEmailVerified = false,

                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),// Hash with email for security
                CodeExpiresAt = DateTime.UtcNow.AddHours(1)
            };

            // In a real app, hash password with Identity’s UserManager; here, we simulate
            _dbContext.DAppUsers.Add(user);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Registered email {Email} for app {AppId}", email, app.Id);
            return new UserOnboardingResponse
            {
                Succeeded = true,
                Message = "Email registered; check your inbox for verification code",
                UserId = user.Id,
                VerificationCode = verificationCode // Simulate sending this via email
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering email {Email} for app {AppId}", email, app.Id);
            return new UserOnboardingResponse
            {
                Succeeded = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<UserOnboardingResponse> ConnectWalletAsync(App app, string walletPublicKey, string signature)
{
    try
    {
        _logger.LogInformation("Attempting to sign up with wallet {Wallet} for AppId {AppId}", walletPublicKey, app.Id);

        if (string.IsNullOrEmpty(walletPublicKey) || string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("Invalid input: WalletPublicKey or Signature is empty for AppId {AppId}", app.Id);
            return new UserOnboardingResponse { Succeeded = false, Message = "Wallet public key or signature is empty" };
        }

        // Create exactly the same message format as in the SDK
        var message = $"Prismon:signup:{app.Id.ToString().ToLower()}:{walletPublicKey}";
        _logger.LogDebug("Message for verification: {Message}", message);

        PublicKey publicKey;
        try
        {
            publicKey = new PublicKey(walletPublicKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid public key: {WalletPublicKey}", walletPublicKey);
            return new UserOnboardingResponse { Succeeded = false, Message = "Invalid public key" };
        }

        byte[] signatureBytes;
        try
        {
            signatureBytes = Encoders.Base58.DecodeData(signature);
            if (signatureBytes.Length != 64)
            {
                _logger.LogWarning("Signature length is {Length}, expected 64 bytes for wallet {Wallet}, AppId {AppId}",
                  signatureBytes.Length, walletPublicKey, app.Id);
                return new UserOnboardingResponse { Succeeded = false, Message = "Invalid signature length" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decode Base58 signature for wallet {Wallet}, AppId {AppId}",
              walletPublicKey, app.Id);
            return new UserOnboardingResponse { Succeeded = false, Message = "Invalid Base58 signature" };
        }

        // Convert message to bytes - exact same as in the login function
        var messageBytes = Encoding.UTF8.GetBytes(message);
        
        // Use the same verification approach as your working login function
        if (!publicKey.Verify(messageBytes, signatureBytes))
        {
            _logger.LogWarning("Invalid signature for wallet {Wallet}, AppId {AppId}", walletPublicKey, app.Id);
            return new UserOnboardingResponse { Succeeded = false, Message = "Invalid signature" };
        }
        _logger.LogDebug("AppId: {AppId}", app.Id);

        var existingUser = await _dbContext.DAppUsers
          .AsNoTracking()
          .FirstOrDefaultAsync(u => u.AppId == app.Id && u.WalletPublicKey == walletPublicKey);

        if (existingUser != null)
        {
            _logger.LogInformation("Wallet {Wallet} already signed up for AppId {AppId}, UserId {UserId}",
              walletPublicKey, app.Id, existingUser.Id);
            return new UserOnboardingResponse
            {
                Succeeded = true,
                Message = "Wallet already signed up for this app",
                UserId = existingUser.Id
            };
        }

        var userId = Guid.NewGuid();
        var user = new DAppUser
        {
            Id = userId,
            WalletPublicKey = walletPublicKey,
            AppId = app.Id,
            IsEmailVerified = false
        };
        _dbContext.DAppUsers.Add(user);

        await _retryPolicy.ExecuteAsync(async () => await _dbContext.SaveChangesAsync());
        _logger.LogInformation("Signed up new DAppUser with Id {UserId}, Wallet {Wallet}, AppId {AppId}",
          userId, walletPublicKey, app.Id);

        return new UserOnboardingResponse
        {
            Succeeded = true,
            Message = "Wallet signed up successfully",
            UserId = userId
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error signing up wallet {Wallet} for AppId {AppId}", walletPublicKey, app.Id);
        return new UserOnboardingResponse
        {
            Succeeded = false,
            Message = $"Error: {ex.Message}"
        };
    }
}
    public async Task<UserOnboardingResponse> VerifyEmailAsync(App app, string email, string verificationCode)
    {
        try
        {
            var user = await _dbContext.DAppUsers
                .FirstOrDefaultAsync(u => u.AppId == app.Id && u.Email == email);

            if (user == null)
            {
                return new UserOnboardingResponse
                {
                    Succeeded = false,
                    Message = "User not found"
                };
            }

            if (user.IsEmailVerified)
            {
                return new UserOnboardingResponse
                {
                    Succeeded = true,
                    Message = "Email already verified",
                    UserId = user.Id
                };
            }

            if (user.CodeExpiresAt < DateTime.UtcNow)
            {
                return new UserOnboardingResponse
                {
                    Succeeded = false,
                    Message = "Verification code expired"
                };
            }

            var hashedInputCode = HashPassword(verificationCode + email);
            if (user.VerificationCode != hashedInputCode)
            {
                return new UserOnboardingResponse
                {
                    Succeeded = false,
                    Message = "Invalid verification code"
                };
            }

            user.IsEmailVerified = true;
            user.VerificationCode = null;
            user.CodeExpiresAt = null;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Verified email {Email} for app {AppId}", email, app.Id);
            return new UserOnboardingResponse
            {
                Succeeded = true,
                Message = "Email verified successfully",
                UserId = user.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying email {Email} for app {AppId}", email, app.Id);
            return new UserOnboardingResponse
            {
                Succeeded = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    private string GenerateVerificationCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(4);
        return Convert.ToBase64String(bytes).Substring(0, 6); // Simple 6-char code
    }

    private string HashPassword(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }
}