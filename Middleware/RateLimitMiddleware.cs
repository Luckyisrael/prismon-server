using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using Prismon.Api.Data;
using Prismon.Api.Models;


namespace Prismon.Api.Middleware
{
    public class RateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitMiddleware> _logger;
        private readonly IDistributedCache _cache;

        public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger, IDistributedCache cache)
        {
            _next = next;
            _logger = logger;
            _cache = cache;
        }

        public async Task InvokeAsync(HttpContext context, PrismonDbContext dbContext)
        {
            if (!context.Request.Path.StartsWithSegments("/devApi"))
            {
                await _next(context);
                return;
            }

            var apiKey = context.Request.Headers["X-API-Key"].ToString();
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("Request rejected: Missing X-API-Key");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Valid X-API-Key required");
                return;
            }

            //var app = await dbContext.Apps.FirstOrDefaultAsync(a => a.ApiKey == apiKey);
            var app = await dbContext.Apps.Include(a => a.Developer).FirstOrDefaultAsync(a => a.ApiKey == apiKey);
            if (app == null)
            {
                _logger.LogWarning("Request rejected: Invalid X-API-Key {ApiKey}", apiKey);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid X-API-Key");
                return;
            }

            // Calculate monthly quota
            int quota = app.Tier switch
            {
                "Premium" => 100_000,
                "Enterprise" => app.CustomRateLimit ?? 500_000,
                _ => 10_000 // Free
            };

            // Check cache for usage count
            int usageCount;
            var cacheKey = $"ApiUsage_{apiKey}_{DateTime.UtcNow:yyyy-MM}";
            try
            {
                var cachedUsage = await _cache.GetStringAsync(cacheKey);
                if (cachedUsage != null)
                {
                    usageCount = JsonSerializer.Deserialize<int>(cachedUsage);
                }
                else
                {
                    // Fallback to database
                    var startDate = DateTime.UtcNow.AddDays(-30);
                    usageCount = await dbContext.ApiUsages
                        .Join(dbContext.Apps,
                            u => u.AppId,
                            a => a.Id,
                            (u, a) => new { u, a })
                        .Where(ua => ua.a.DeveloperId == app.DeveloperId && ua.u.Timestamp >= startDate)
                        .CountAsync();

                    // Cache for 1 hour
                    await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(usageCount), new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis unavailable, falling back to database for {ApiKey}", apiKey);
                var startDate = DateTime.UtcNow.AddDays(-30);
                usageCount = await dbContext.ApiUsages
                    .Join(dbContext.Apps,
                        u => u.AppId,
                        a => a.Id,
                        (u, a) => new { u, a })
                    .Where(ua => ua.a.DeveloperId == app.DeveloperId && ua.u.Timestamp >= startDate)
                    .CountAsync();
            }

            if (usageCount >= quota)
            {
                _logger.LogWarning("Rate limit exceeded for {ApiKey}: {Usage}/{Quota} calls", apiKey, usageCount, quota);
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsync("Monthly API call quota exceeded. Please upgrade your plan or try again next month.");
                return;
            }

            // Log usage
            dbContext.ApiUsages.Add(new ApiUsage
            {
                Id = Guid.NewGuid(),
                AppId = app.Id,
                Timestamp = DateTime.UtcNow,
                Endpoint = context.Request.Path
            });
            await dbContext.SaveChangesAsync();

            // Update cache 
            try
            {
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(usageCount + 1), new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update Redis cache for {ApiKey}", apiKey);
            }

            _logger.LogInformation("API call logged for {ApiKey}: {Usage}/{Quota} calls", apiKey, usageCount + 1, quota);
            await _next(context);
        }
    }
}