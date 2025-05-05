namespace Prismon.Api.Controllers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prismon.Api.Data;
using Prismon.Api.Interface;
using Prismon.Api.Models;

[Route("devApi/[controller]")]
[ApiController]

public class AIController : ControllerBase
{
    private readonly IAIService _aiService;
    private readonly ILogger<AIController> _logger;
    private readonly PrismonDbContext _dbContext;

    // Ensure this is the ONLY constructor for this class
    public AIController(IAIService aiService, ILogger<AIController> logger, PrismonDbContext dbContext)
    {
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    [HttpPost("invoke")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> InvokeAI([FromBody] AIInvokeRequest request)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid AI invoke request for UserId {UserId}", request.UserId);
            return BadRequest(ModelState);
        }

        try
        {
            var response = await _aiService.InvokeAIAsync(request);
            if (!response.Succeeded)
            {
                _logger.LogWarning("AI invocation failed for UserId {UserId}: {Message}", request.UserId, response.Message);
                return BadRequest(response);
            }

            _logger.LogInformation("AI invocation successful for UserId {UserId}, ModelId {ModelId}", request.UserId, request.ModelId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking AI for UserId {UserId}, ModelId {ModelId}", request.UserId, request.ModelId);
            return StatusCode(500, new { Message = "Failed to invoke AI" });
        }
    }

    [HttpPost("models")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegisterModel([FromBody] AIModelConfig config)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid model registration request");
            return BadRequest(ModelState);
        }
        var app = await GetAppFromApiKey();
        if (app == null)
        {
            _logger.LogWarning("Invalid API key: {ApiKey}", Request.Headers["X-API-Key"]);
            return Unauthorized("Invalid API key");
        }

        try
        {
            Guid parsedAppId;
            var appIdStr = app.Id.ToString();
            var appId = Guid.TryParse(appIdStr, out parsedAppId) ? appIdStr : null;

            if (string.IsNullOrEmpty(appId) || !Guid.TryParse(appId, out _))
            {
                _logger.LogError("Invalid AppId in JWT");
                return Unauthorized("Invalid user session");
            }

            var registerResponse = await _aiService.RegisterModelAsync(appId, config);
            if (!registerResponse.Succeeded)
            {
                _logger.LogWarning("Model registration failed for AppId {AppId}", appId);
                return BadRequest(new { Message = registerResponse.Message ?? "Failed to register model" });
            }

            _logger.LogInformation("Model registered successfully for AppId {AppId}", appId);
            return Ok(new { Message = "Model registered successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering model for AppId");
            return StatusCode(500, new { Message = "Failed to register model" });
        }
    }

    [HttpGet("models/{modelId}/exists")]
    public async Task<IActionResult> ModelExists(string modelId)
    {
        var app = await GetAppFromApiKey();
        if (app == null) return Unauthorized();

        var exists = await _dbContext.AIModels
            .AnyAsync(m => m.Id == modelId && m.AppId == app.Id);

        return Ok(new { Exists = exists });
    }
    private async Task<App?> GetAppFromApiKey()
    {
        var apiKey = Request.Headers["X-API-Key"].ToString();
        return await _dbContext.Apps.FirstOrDefaultAsync(a => a.ApiKey == apiKey);
    }
}
