
using Microsoft.EntityFrameworkCore;
using Prismon.Api.Data;
using System.Text;

namespace Prismon.Api.Middleware;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private const string ApiKeyHeader = "X-API-Key";

    public ApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, PrismonDbContext dbContext)
    {
        // Skip validation for auth and app creation endpoints (JWT-based)
        var path = context.Request.Path.Value?.ToLower();
        if (path != null && (path.StartsWith("/api/auth") || path.StartsWith("/api/apps") || path.StartsWith("/api/organizations")))
        {
            await _next(context);
            return;
        }

        // Convert StringValues to string
        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var apiKeyValues) || string.IsNullOrEmpty(apiKeyValues.ToString()))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("API Key is missing");
            return;
        }

        var apiKey = apiKeyValues.ToString(); // Extract the string value

        var app = await dbContext.Apps
            .FirstOrDefaultAsync(a => a.ApiKey == apiKey);

        if (app == null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Invalid API Key");
            return;
        }

        // Add app context to HttpContext for downstream use
        context.Items["App"] = app;

        await _next(context);
    }
}

public static class ApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyMiddleware>();
    }
}
