using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prismon.Api.Data;

namespace Prismon.Api.Services
{
    public class ApiUsageCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ApiUsageCleanupService> _logger;

        public ApiUsageCleanupService(IServiceProvider serviceProvider, ILogger<ApiUsageCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<PrismonDbContext>();

                    var cutoff = DateTime.UtcNow.AddDays(-30);
                    var oldRecords = await dbContext.ApiUsages
                        .Where(u => u.Timestamp < cutoff)
                        .ToListAsync(stoppingToken);

                    if (oldRecords.Any())
                    {
                        dbContext.ApiUsages.RemoveRange(oldRecords);
                        await dbContext.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Cleaned up {Count} old ApiUsage records", oldRecords.Count);
                    }

                    await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during ApiUsage cleanup");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
        }
    }
}