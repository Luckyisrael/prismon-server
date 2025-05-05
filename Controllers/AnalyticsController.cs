using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prismon.Api.Data;
using Prismon.Api.Interface;
using Prismon.Api.Models;
using Prismon.Api.Services;
using System.Security.Claims;

namespace Prismon.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUserActivity([FromQuery] Guid appId)
    {
        var developerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(developerId))
        {
            return Unauthorized(new { Message = "User not authenticated" });
        }

        try
        {
            var activity = await _analyticsService.GetUserActivityAsync(appId, developerId);
            return Ok(activity);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { Message = ex.Message });
        }
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetUserTransactions([FromQuery] string userId, [FromHeader(Name = "X-API-Key")] string apiKey)
    {
        var app = await GetAppFromApiKey(apiKey);
        if (app == null)
        {
            return Unauthorized(new { Message = "Invalid API key" });
        }

        var transactions = await _analyticsService.GetUserTransactionsAsync(userId, app.Id);
        return Ok(transactions);
    }

    private async Task<App?> GetAppFromApiKey(string apiKey)
    {
        var dbContext = HttpContext.RequestServices.GetRequiredService<PrismonDbContext>();
        return await dbContext.Apps.FirstOrDefaultAsync(a => a.ApiKey == apiKey);
    }
}