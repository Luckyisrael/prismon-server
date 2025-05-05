using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Prismon.Api.Interface;
using Prismon.Api.Models;
using Prismon.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Prismon.Api.Controllers
{
    [Route("devApi/Pyth/price")]
    [ApiController]
    public class PythPriceFeedController : ControllerBase
    {
        private readonly IPythPriceFeedService _priceFeedService;
        private readonly PrismonDbContext _dbContext;
        private readonly ILogger<IPythPriceFeedService> _logger;

        public PythPriceFeedController(IPythPriceFeedService priceFeedService, PrismonDbContext dbContext, ILogger<IPythPriceFeedService> logger)
        {
            _priceFeedService = priceFeedService;
            _dbContext = dbContext;
             _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        }

        private async Task<App?> GetAppFromApiKey()
        {
            var apiKey = Request.Headers["X-API-Key"].ToString(); 
            return await _dbContext.Apps.FirstOrDefaultAsync(a => a.ApiKey == apiKey);
        }

        [HttpGet("feeds")]
        public async Task<IActionResult> GetPriceFeeds([FromQuery] string? query, [FromQuery] string? assetType)
        {
            var app = await GetAppFromApiKey();
            if (app == null)
            {
                _logger.LogWarning("Invalid API key: {ApiKey}", Request.Headers["X-API-Key"]);
                return Unauthorized("Invalid API key");
            }
            try
            {
                var request = new PriceFeedRequest { Query = query, AssetType = assetType };
                var feeds = await _priceFeedService.GetPriceFeedsAsync(request);
                return Ok(new { Feeds = feeds });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("latest")]
        public async Task<IActionResult> GetLatestPrice([FromQuery] List<string> ids, [FromQuery] bool ignoreInvalidPriceIds = true)
        {
            var app = await GetAppFromApiKey();
            if (app == null)
            {
                _logger.LogWarning("Invalid API key: {ApiKey}", Request.Headers["X-API-Key"]);
                return Unauthorized("Invalid API key");
            }
            try
            {
                if (ids == null || ids.Count == 0)
                    return BadRequest("At least one price feed ID is required");

                var request = new LatestPriceRequest { PriceFeedIds = ids, IgnoreInvalidPriceIds = ignoreInvalidPriceIds };
                var prices = await _priceFeedService.GetLatestPriceAsync(request);
                return Ok(new { Prices = prices });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("stream/start")]
        public async Task<IActionResult> StartPriceStream([FromBody] StreamPriceRequest request, [FromQuery] string sessionId)
        {
            var app = await GetAppFromApiKey();
            if (app == null)
            {
                _logger.LogWarning("Invalid API key: {ApiKey}", Request.Headers["X-API-Key"]);
                return Unauthorized("Invalid API key");
            }
            try
            {
                if (string.IsNullOrEmpty(sessionId) || request.PriceFeedIds.Count == 0)
                    return BadRequest("Session ID and at least one price feed ID are required");

                await _priceFeedService.StartPriceStreamAsync(sessionId, request, async (priceUpdate) =>
                {
                    // In a real implementation, push updates to the client via SignalR or a client-side HTTP stream
                    Console.WriteLine($"Price update for session {sessionId}: {System.Text.Json.JsonSerializer.Serialize(priceUpdate)}");
                });

                return Ok(new { Message = "Streaming started" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("stream/stop")]
        public async Task<IActionResult> StopPriceStream([FromQuery] string sessionId)
        {
            var app = await GetAppFromApiKey();
            if (app == null)
            {
                _logger.LogWarning("Invalid API key: {ApiKey}", Request.Headers["X-API-Key"]);
                return Unauthorized("Invalid API key");
            }
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                    return BadRequest("Session ID is required");

                await _priceFeedService.StopPriceStreamAsync(sessionId);
                return Ok(new { Message = "Streaming stopped" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }
    }
}