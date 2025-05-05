using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Prismon.Api.Data;
using Prismon.Api.DTOs;
using Prismon.Api.Interface;
using Prismon.Api.Models;

namespace Prismon.Api.Services;

public class AppService : IAppService
{
    private readonly PrismonDbContext _dbContext;
    private readonly ILogger<AppService> _logger;
    private readonly UserManager<ApplicationUser> _userManager;

    public AppService(PrismonDbContext dbContext, ILogger<AppService> logger, UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _logger = logger;
        _userManager = userManager;
    }

    public async Task<AppResponse> CreateAppAsync(CreateAppRequest request, string developerId, Guid? organizationId = null)
    {
        if (request == null || string.IsNullOrEmpty(request.Name) || string.IsNullOrEmpty(developerId))
        {
            return new AppResponse { Succeeded = false, Message = "Name and DeveloperId are required." };
        }

        var developer = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == developerId); // Use string directly

        if (developer == null)
        {
            _logger.LogWarning("Developer not found for ID: {DeveloperId}", developerId);
            return new AppResponse { Succeeded = false, Message = "Developer not found." };
        }

        var app = new App
        {
            Name = request.Name,
            ApiKey = $"prismon_{Guid.NewGuid().ToString("N")[..8]}_app{Guid.NewGuid().ToString("N")[..8]}",
            DeveloperId = developer.Id,
            OrganizationId = organizationId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Apps.Add(app);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("App {AppId} created by developer {DeveloperId}", app.Id, developerId);

        return new AppResponse
        {
            Succeeded = true,
            Message = "App created successfully",
            Id = app.Id,
            Name = app.Name,
            ApiKey = app.ApiKey,
            DeveloperId = app.DeveloperId,
            OrganizationId = app.OrganizationId,
            CreatedAt = app.CreatedAt,
            ProgramId = app.ProgramId,
            DeployedEndpoint = app.DeployedEndpoint,
            DeployedAt = app.DeployedAt
        };
    }

    public async Task<List<AppResponse>> GetAppsAsync(string developerId)
    {
        if (string.IsNullOrEmpty(developerId))
        {
            throw new ArgumentException("DeveloperId is required.");
        }

        var apps = await _dbContext.Apps
            .Where(a => a.DeveloperId == developerId)
            .Select(a => new AppResponse
            {
                Id = a.Id,
                Name = a.Name,
                ApiKey = a.ApiKey,
                DeveloperId = a.DeveloperId,
                OrganizationId = a.OrganizationId,
                CreatedAt = a.CreatedAt,
                ProgramId = a.ProgramId,
                DeployedEndpoint = a.DeployedEndpoint,
                DeployedAt = a.DeployedAt
            })
            .ToListAsync();

        return apps;
    }

    public async Task<AppResponse?> GetAppAsync(Guid appId, string developerId)
    {
        if (string.IsNullOrEmpty(developerId))
        {
            throw new ArgumentException("DeveloperId is required.");
        }

        var app = await _dbContext.Apps
            .Where(a => a.Id == appId && a.DeveloperId == developerId)
            .Select(a => new AppResponse
            {
                Id = a.Id,
                Name = a.Name,
                ApiKey = a.ApiKey,
                DeveloperId = a.DeveloperId,
                OrganizationId = a.OrganizationId,
                CreatedAt = a.CreatedAt,
                ProgramId = a.ProgramId,
                DeployedEndpoint = a.DeployedEndpoint,
                DeployedAt = a.DeployedAt
            })
            .FirstOrDefaultAsync();

        return app;
    }

 public async Task<AppResponse> UpdateAppAsync(Guid appId, UpdateAppRequest request, string developerId)
    {
        var app = await _dbContext.Apps
            .FirstOrDefaultAsync(a => a.Id == appId && a.DeveloperId == developerId);
        if (app == null)
        {
            return new AppResponse { Succeeded = false, Message = "App not found or not owned by developer" };
        }

        app.Name = request.Name;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("App {AppId} updated by developer {DeveloperId}", appId, developerId);
        return MapToResponse(app, true, "App updated successfully");
    }
    public async Task<AppResponse> DeleteAppAsync(Guid appId, string developerId)
    {
        var app = await _dbContext.Apps
            .FirstOrDefaultAsync(a => a.Id == appId && a.DeveloperId == developerId);
        if (app == null)
        {
            return new AppResponse { Succeeded = false, Message = "App not found or not owned by developer" };
        }

        _dbContext.Apps.Remove(app);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("App {AppId} deleted by developer {DeveloperId}", appId, developerId);
        return new AppResponse { Succeeded = true, Message = "App deleted successfully" };
    }

     public async Task<AppResponse> RegenerateApiKeyAsync(Guid appId, string developerId)
    {
        var app = await _dbContext.Apps
            .FirstOrDefaultAsync(a => a.Id == appId && a.DeveloperId == developerId);
        if (app == null)
        {
            return new AppResponse { Succeeded = false, Message = "App not found or not owned by developer" };
        }

        app.ApiKey = $"prismon_{Guid.NewGuid().ToString("N")[..8]}_app{Guid.NewGuid().ToString("N")[..8]}";
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("API key regenerated for app {AppId} by developer {DeveloperId}", appId, developerId);
        return MapToResponse(app, true, "API key regenerated successfully");
    }

    public async Task<OrganizationResponse> CreateOrganizationAsync(CreateOrganizationRequest request, string developerId)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.DeveloperId == Guid.Parse(developerId))
            ?? throw new InvalidOperationException("Developer not found");

        if (user.OrganizationId.HasValue)
            throw new InvalidOperationException("User already belongs to an organization");

        var org = new Organization
        {
            Name = request.Name,
            OwnerId = user.Id,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Organizations.Add(org);
        user.OrganizationId = org.Id;
        user.IsOnboardingComplete = true;
        await _dbContext.SaveChangesAsync();

        return new OrganizationResponse
        {
            Id = org.Id,
            Name = org.Name,
            OwnerId = Guid.Parse(org.OwnerId),
            CreatedAt = org.CreatedAt
        };
    }

    public async Task<bool> CompleteOnboardingAsync(string developerId)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.DeveloperId == Guid.Parse(developerId))
            ?? throw new InvalidOperationException("Developer not found");

        if (user.IsOnboardingComplete)
            return true;

        user.IsOnboardingComplete = true;
        await _dbContext.SaveChangesAsync();
        return true;
    }

     private static AppResponse MapToResponse(App app, bool succeeded, string message)
    {
        return new AppResponse
        {
            Succeeded = succeeded,
            Message = message,
            Id = app.Id,
            Name = app.Name,
            ApiKey = app.ApiKey,
            DeveloperId = app.DeveloperId,
            OrganizationId = app.OrganizationId,
            CreatedAt = app.CreatedAt,
            ProgramId = app.ProgramId,
            DeployedEndpoint = app.DeployedEndpoint,
            DeployedAt = app.DeployedAt
        };
    }
}