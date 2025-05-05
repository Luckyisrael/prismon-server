using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Prismon.Api.Data;
using Prismon.Api.DTOs;
using Prismon.Api.Models;
using Solnet.Wallet;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Solnet.Wallet.Utilities;
using BCrypt.Net;
using Prismon.Api.Interface;

namespace Prismon.Api.Services;

public class UserAuthService : IUserAuthService
{
    private readonly PrismonDbContext _dbContext;
    private readonly ILogger<UserAuthService> _logger;
    private readonly IConfiguration _configuration;

    public UserAuthService(PrismonDbContext dbContext, ILogger<UserAuthService> logger, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<LoginResponse> LoginWithEmailAsync(string email, string password, Guid appId)
    {
        var user = await _dbContext.DAppUsers
            .FirstOrDefaultAsync(u => u.AppId == appId && u.Email == email && u.IsEmailVerified);
        if (user == null)
        {
            return new LoginResponse { Succeeded = false, Message = "User not found or email not verified" };
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return new LoginResponse { Succeeded = false, Message = "Invalid password" };
        }

        var token = GenerateJwtToken(user);
        _logger.LogInformation("User {UserId} logged in with email for app {AppId}", user.Id, appId);
        return new LoginResponse
        {
            Succeeded = true,
            Message = "Login successful",
            UserId = user.Id.ToString(),
            Token = token
        };
    }
public async Task<LoginResponse> LoginWithWalletAsync(string walletPublicKey, string signature, Guid appId, Guid challengeId)
{
    _logger.LogInformation("Attempting login for wallet {Wallet}, AppId {AppId}, ChallengeId {ChallengeId}", 
        walletPublicKey, appId, challengeId);

    // Verify app exists
    var app = await _dbContext.Apps.FirstOrDefaultAsync(a => a.Id == appId);
    if (app == null)
    {
        _logger.LogWarning("Invalid AppId {AppId}", appId);
        return new LoginResponse { Succeeded = false, Message = "Invalid app ID" };
    }

    var user = await _dbContext.DAppUsers
        .AsNoTracking()
        .FirstOrDefaultAsync(u => u.AppId == appId && u.WalletPublicKey == walletPublicKey);
    if (user == null)
    {
        _logger.LogWarning("No DAppUser found for wallet {Wallet} and AppId {AppId}. Please sign up first.", 
            walletPublicKey, appId);
        return new LoginResponse { Succeeded = false, Message = "Wallet not signed up for this app. Please sign up via /users/connect-wallet." };
    }

    var challengeEntity = await _dbContext.LoginChallenges
        .FirstOrDefaultAsync(c => c.Id == challengeId && c.AppId == appId && 
                                c.WalletPublicKey == walletPublicKey && c.ExpiresAt > DateTime.UtcNow);
    if (challengeEntity == null)
    {
        _logger.LogWarning("Invalid or expired challenge for ChallengeId {ChallengeId}, AppId {AppId}, Wallet {Wallet}", 
            challengeId, appId, walletPublicKey);
        return new LoginResponse { Succeeded = false, Message = "Invalid or expired challenge; request a new one via /users/challenge." };
    }

    var publicKey = new PublicKey(walletPublicKey);
    var signatureBytes = Encoders.Base58.DecodeData(signature);
    var messageBytes = Encoding.UTF8.GetBytes(challengeEntity.Challenge);

    if (!publicKey.Verify(messageBytes, signatureBytes))
    {
        _logger.LogWarning("Invalid signature for wallet {Wallet}, ChallengeId {ChallengeId}", walletPublicKey, challengeId);
        return new LoginResponse { Succeeded = false, Message = "Invalid signature" }; 
    }

    _dbContext.LoginChallenges.Remove(challengeEntity);
    await _dbContext.SaveChangesAsync();

    var token = GenerateJwtToken(user);
    _logger.LogInformation("User {UserId} logged in with wallet {Wallet} for app {AppId}", user.Id, walletPublicKey, appId);
    return new LoginResponse
    {
        Succeeded = true,
        Message = "Login successful",
        UserId = user.Id.ToString(),
        UserWallet = user.WalletPublicKey,
        Token = token
    };
}
    private string GenerateJwtToken(DAppUser user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.Email ?? user.WalletPublicKey ?? "anonymous"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("AppId", user.AppId.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddDays(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}