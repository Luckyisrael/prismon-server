using Microsoft.EntityFrameworkCore;
using Prismon.Api.Data;
using Prismon.Api.DTOs;
using Prismon.Api.Interface;
using Prismon.Api.Models;
using Solnet.Rpc;

namespace Prismon.Api.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly PrismonDbContext _dbContext;
    private readonly ILogger<AnalyticsService> _logger;
    private readonly IRpcClient _rpcClient;

    public AnalyticsService(PrismonDbContext dbContext, ILogger<AnalyticsService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
        _rpcClient = ClientFactory.GetClient(Cluster.DevNet); // Use DevNet for now
    }

    public async Task<UserActivityDto> GetUserActivityAsync(Guid appId, string developerId)
    {
        var app = await _dbContext.Apps
            .FirstOrDefaultAsync(a => a.Id == appId && a.DeveloperId == developerId);
        if (app == null)
        {
            throw new UnauthorizedAccessException("App not found or not owned by developer.");
        }

        var users = await _dbContext.DAppUsers
            .Where(u => u.AppId == appId)
            .ToListAsync();

        var totalUsers = users.Count;
        var activeUsersLast24h = users.Count(u => u.CreatedAt >= DateTime.UtcNow.AddHours(-24));
        var registrationsLast7d = users.Count(u => u.CreatedAt >= DateTime.UtcNow.AddDays(-7));

        _logger.LogInformation("Fetched activity for app {AppId}: {TotalUsers} users", appId, totalUsers);

        return new UserActivityDto
        {
            AppId = appId,
            TotalUsers = totalUsers,
            ActiveUsersLast24h = activeUsersLast24h,
            RegistrationsLast7d = registrationsLast7d
        };
    }

    public async Task<List<TransactionDto>> GetUserTransactionsAsync(string userId, Guid appId)
    {
        var user = await _dbContext.DAppUsers
            .FirstOrDefaultAsync(u => u.Id.ToString() == userId && u.AppId == appId);
        if (user == null || string.IsNullOrEmpty(user.WalletPublicKey))
        {
            _logger.LogWarning("User {UserId} not found or no wallet connected for app {AppId}", userId, appId);
            return new List<TransactionDto>();
        }

        var signatures = await _rpcClient.GetSignaturesForAddressAsync(user.WalletPublicKey);
        if (!signatures.WasSuccessful)
        {
            _logger.LogError("Failed to fetch signatures for wallet {Wallet}: {Error}", user.WalletPublicKey, signatures.RawRpcResponse);
            return new List<TransactionDto>();
        }

        var transactions = new List<TransactionDto>();
        foreach (var sig in signatures.Result.Take(5)) // Limit to 5 for simplicity
        {
            var tx = await _rpcClient.GetTransactionAsync(sig.Signature ); //maxSupportedTransactionVersion: 0);
            if (tx.WasSuccessful && tx.Result != null)
            {
                transactions.Add(new TransactionDto
                {
                    Signature = sig.Signature,
                    Amount = "N/A", // Placeholder; calculate SOL amount later
                    Timestamp = tx.Result.BlockTime.HasValue
                        ? DateTimeOffset.FromUnixTimeSeconds(tx.Result.BlockTime.Value).UtcDateTime
                        : null
                });
            }
        }

        _logger.LogInformation("Fetched {Count} transactions for user {UserId}", transactions.Count, userId);
        return transactions;
    }
}