/***
using Microsoft.EntityFrameworkCore;
using Prismon.Api.Data;

public class ApiKeyClientResolver : IClientResolver
{
    private readonly IHttpContextAccessor _accessor;
    private readonly PrismonDbContext _dbContext;

    public ApiKeyClientResolver(IHttpContextAccessor accessor, PrismonDbContext dbContext)
    {
        _accessor = accessor;
        _dbContext = dbContext;
    }

    public async Task<string> ResolveClientAsync()
    {
        var apiKey = _accessor.HttpContext?.Request.Headers["X-API-Key"].ToString();
        if (string.IsNullOrEmpty(apiKey)) return "anonymous";

        var app = await _dbContext.Apps.FirstOrDefaultAsync(a => a.ApiKey == apiKey);
        return app != null ? $"{app.Tier}_{app.ApiKey}" : "anonymous";
    }
}

public interface IClientResolver
{
}

**/