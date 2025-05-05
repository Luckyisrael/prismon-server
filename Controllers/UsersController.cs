using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prismon.Api.Data;
using Prismon.Api.DTOs;
using Prismon.Api.Interface;
using Prismon.Api.Models;
using Solnet.Wallet.Utilities;
using Solnet.Wallet;


namespace Prismon.Api.Controllers;

[ApiController]
[Route("devApi/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserOnboardingService _onboardingService;
    private readonly IUserAuthService _authService;
    private readonly IUserProfileService _profileService;
    private readonly PrismonDbContext _dbContext;
    private readonly ILogger<IUserOnboardingService> _logger;

    public UsersController(IUserOnboardingService onboardingService, IUserAuthService authService, IUserProfileService profileService, PrismonDbContext dbContext, ILogger<IUserOnboardingService> logger)
    {
        _onboardingService = onboardingService;
        _authService = authService;
        _profileService = profileService;
        _dbContext = dbContext;
        _logger = logger;

    }


    [HttpPost("connect-wallet")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserOnboardingResponse>> ConnectWallet([FromBody] ConnectWalletRequest request)
    {
        var app = await GetAppFromApiKey();
        if (app == null)
        {
            _logger.LogWarning("Invalid API key: {ApiKey}", Request.Headers["X-API-Key"]);
            return Unauthorized("Invalid API key");
        }

        var result = await _onboardingService.ConnectWalletAsync(app, request.WalletPublicKey, request.Signature);
        if (!result.Succeeded)
        {
            return BadRequest(result.Message);
        }

        return Ok(result);
    }

    public class ConnectWalletRequest
    {
        public string WalletPublicKey { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }


    [HttpPost("register-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RegisterEmail([FromBody] RegisterEmailRequest request)
    {
        //var app = HttpContext.Items["App"] as App;
        var app = await GetAppFromApiKey();
        if (app == null)
        {
            return BadRequest(new { Message = "App context not found" });
        }

        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new { Message = "Email and password are required" });
        }

        var response = await _onboardingService.RegisterEmailAsync(app, request.Email, request.Password);
        if (!response.Succeeded)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("verify-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        //var app = HttpContext.Items["App"] as App;
        var app = await GetAppFromApiKey();
        if (app == null)
        {
            return BadRequest(new { Message = "App context not found" });
        }

        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.VerificationCode))
        {
            return BadRequest(new { Message = "Email and verification code are required" });
        }

        var response = await _onboardingService.VerifyEmailAsync(app, request.Email, request.VerificationCode);
        if (!response.Succeeded)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }
    [HttpPost("login-email")]
    public async Task<IActionResult> LoginEmail([FromBody] LoginEmailRequest request)
    {
        var app = await GetAppFromApiKey();
        if (app == null)
        {
            return BadRequest(new { Message = "Invalid API key" });
        }

        var response = await _authService.LoginWithEmailAsync(request.Email, request.Password, app.Id);
        return response.Succeeded ? Ok(response) : BadRequest(response);
    }

    [HttpPost("login-wallet")]
    public async Task<IActionResult> LoginWallet([FromBody] LoginWalletRequest request)
    {
        var app = await GetAppFromApiKey();
        if (app == null)
        {
            return BadRequest(new { Message = "Invalid API key" });
        }

        var response = await _authService.LoginWithWalletAsync(request.WalletPublicKey, request.Signature, app.Id, request.ChallengeId);
        return response.Succeeded ? Ok(response) : BadRequest(response);
    }

    [HttpGet("challenge")]
    public async Task<IActionResult> GetChallenge([FromQuery] string walletPublicKey)
    {
        if (string.IsNullOrEmpty(walletPublicKey))
        {
            return BadRequest(new { Message = "Wallet public key is required" });
        }

        var app = await GetAppFromApiKey();
        if (app == null)
        {
            return BadRequest(new { Message = "Invalid API key" });
        }

        // Generate and store challenge
        var challenge = $"Prismon Login: {Guid.NewGuid().ToString("N")}"; // Unique challenge
        var challengeEntity = new LoginChallenge
        {
            AppId = app.Id,
            WalletPublicKey = walletPublicKey,
            Challenge = challenge
        };

        _dbContext.LoginChallenges.Add(challengeEntity);
        await _dbContext.SaveChangesAsync();

        return Ok(new { Challenge = challenge, ChallengeId = challengeEntity.Id });
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var appId = Guid.Parse(User.FindFirst("AppId")?.Value ?? throw new InvalidOperationException("AppId missing"));
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { Message = "User not authenticated" });
        }

        var response = await _profileService.UpdateProfileAsync(userId, appId, request);
        return response.Succeeded ? Ok(response) : BadRequest(response);
    }

    [Authorize]
    [HttpDelete("wallet")]
    public async Task<IActionResult> DisconnectWallet()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var appId = Guid.Parse(User.FindFirst("AppId")?.Value ?? throw new InvalidOperationException("AppId missing"));
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { Message = "User not authenticated" });
        }

        var response = await _profileService.DisconnectWalletAsync(userId, appId);
        return response.Succeeded ? Ok(response) : BadRequest(response);
    }

    [HttpGet("debug/wallet")]
    public IActionResult DebugWallet()
    {
        var walletAddress = User.FindFirst("walletAddress")?.Value;
        return Ok(new { walletAddress = walletAddress });
    }
    private async Task<App?> GetAppFromApiKey()
    {
        var apiKey = Request.Headers["X-API-Key"].ToString();
        return await _dbContext.Apps.FirstOrDefaultAsync(a => a.ApiKey == apiKey);
    }
}

public class ConnectWalletRequest
{
    public string WalletPublicKey { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}

public class RegisterEmailRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class VerifyEmailRequest
{
    public string Email { get; set; } = string.Empty;
    public string VerificationCode { get; set; } = string.Empty;
}