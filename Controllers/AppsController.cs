using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prismon.Api.DTOs;
using Prismon.Api.Interface;
using Prismon.Api.Services;
using System.Security.Claims;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prismon.Api.Data;
using Prismon.Api.Services;
using System.Security.Claims;

namespace Prismon.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AppsController : ControllerBase
{
    private readonly IAppService _appService;
    private readonly PrismonDbContext _dbContext;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<AppsController> _logger;

    public AppsController(PrismonDbContext dbContext, IPaymentService paymentService, ILogger<AppsController> logger, IAppService appService)
    {
        _appService = appService;
        _dbContext = dbContext;
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateApp([FromBody] CreateAppRequest request)
    {
        var developerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(developerId))
        {
            return Unauthorized(new { Message = "User not authenticated" });
        }

        var response = await _appService.CreateAppAsync(request, developerId);
        if (response.Succeeded != true)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetApps()
    {
        var developerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(developerId))
        {
            return Unauthorized(new { Message = "User not authenticated" });
        }

        var apps = await _appService.GetAppsAsync(developerId);
        return Ok(apps);
    }

    [HttpPut("{appId}")]
    public async Task<IActionResult> UpdateApp(Guid appId, [FromBody] UpdateAppRequest request)
    {
        var developerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(developerId))
        {
            return Unauthorized(new { Message = "User not authenticated" });
        }

        var response = await _appService.UpdateAppAsync(appId, request, developerId);
        return response.Succeeded == true ? Ok(response) : BadRequest(response);
    }

    [HttpDelete("{appId}")]
    public async Task<IActionResult> DeleteApp(Guid appId)
    {
        var developerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(developerId))
        {
            return Unauthorized(new { Message = "User not authenticated" });
        }

        var response = await _appService.DeleteAppAsync(appId, developerId);
        return response.Succeeded == true ? Ok(response) : BadRequest(response);
    }
    [HttpPost("{appId}/regenerate-key")]
    public async Task<IActionResult> RegenerateApiKey(Guid appId)
    {
        var developerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(developerId))
        {
            return Unauthorized(new { Message = "User not authenticated" });
        }

        var response = await _appService.RegenerateApiKeyAsync(appId, developerId);
        return response.Succeeded == true ? Ok(response) : BadRequest(response);
    }

    [HttpPost("upgrade")]
    [Authorize]
    public async Task<IActionResult> UpgradeUser([FromBody] UpgradeUserRequest request)
    {
        try
        {
            // Validate tier
            if (request.Tier != "Premium" && request.Tier != "Enterprise")
                return BadRequest("Invalid tier. Must be 'Premium' or 'Enterprise'.");

            if (request.Tier == "Enterprise" && !request.CustomRateLimit.HasValue)
                return BadRequest("CustomRateLimit is required for Enterprise tier.");

            // Get user from JWT
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Invalid user ID in token.");

            var user = await _dbContext.Users.FindAsync(userId); // Changed to use string directly
            if (user == null)
                return NotFound("User not found.");

            if (user.Tier == request.Tier)
                return BadRequest($"User is already on {user.Tier} tier.");


            // Determine amount and currency
            var config = HttpContext.RequestServices.GetRequiredService<IOptions<PaystackConfig>>().Value;
            decimal amount;
            string currency;

            if (request.Currency == "NGN")
            {
                amount = request.Tier == "Premium" ? 40000 : 0; // Enterprise pricing TBD
                currency = "NGN";
            }
            else
            {
                amount = request.Tier == "Premium" ? 30 : 0; // Enterprise pricing TBD
                currency = "USD";
            }

            if (amount == 0)
                return BadRequest("Pricing not available for selected tier and currency.");

            // Initialize payment
            var reference = $"prismon_user_{userId}_{DateTime.UtcNow.Ticks}";
            var paymentUrl = await _paymentService.InitializePaymentAsync(user.Email, request.Tier, amount, currency, reference);

            // Save pending upgrade
            user.Tier = request.Tier;
            user.CustomRateLimit = request.Tier == "Enterprise" ? request.CustomRateLimit : null;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Upgrade initiated for User {UserId} to {Tier}, Payment Reference: {Reference}", userId, request.Tier, reference);
            return Ok(new { PaymentUrl = paymentUrl, Reference = reference });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate upgrade for User {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return StatusCode(500, "An error occurred while processing the upgrade.");
        }
    }


    [HttpGet("payment/callback")]

    public async Task<IActionResult> PaymentCallback([FromQuery] string reference)
    {
        try
        {
            if (string.IsNullOrEmpty(reference))
                return BadRequest("Payment reference is required.");

            var isVerified = await _paymentService.VerifyPaymentAsync(reference);
            if (!isVerified)
            {
                _logger.LogWarning("Payment verification failed for Reference: {Reference}", reference);
                return BadRequest("Payment verification failed.");
            }

            // Extract the user ID part (which is a string)
            var userIdString = reference.Split('_')[2];

            // Find the user using the string ID
            var user = await _dbContext.Users.FindAsync(userIdString);
            if (user == null)
                return NotFound("User not found.");

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Payment verified and upgrade completed for User {UserId} to {Tier}", userIdString, user.Tier);
            return Redirect("https://prismon-api.azurewebsites.net");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process payment callback for Reference: {Reference}", reference);
            return Redirect("https://prismon-api.azurewebsites.net");
        }
    }
}

public class UpgradeUserRequest
{
    public string Tier { get; set; } = string.Empty;
    public int? CustomRateLimit { get; set; }
    public string Currency { get; set; } = "NGN"; // NGN or USD
}