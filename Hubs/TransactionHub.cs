/**using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Prismon.Api.Data;
using Prismon.Api.Interface;
using Prismon.Api.Models;
using System.Security.Claims;

namespace Prismon.Api.Hubs;

[Authorize]
public class TransactionHub : Hub
{
    private readonly ISolanaService _solanaService;
    private readonly PrismonDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly ILogger<TransactionHub> _logger;

    public TransactionHub(ISolanaService solanaService, PrismonDbContext dbContext, IEmailService emailService, ILogger<TransactionHub> logger)
    {
        _solanaService = solanaService;
        _dbContext = dbContext;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task SubscribeToTransactions(string userId, string apiKey, TransactionFilter filter)
    {
        try
        {
            var app = await _dbContext.Apps.FirstOrDefaultAsync(a => a.ApiKey == apiKey);
            if (app == null)
            {
                await Clients.Caller.SendAsync("Error", "Invalid API key");
                return;
            }

            var dappUser = await _dbContext.DAppUsers.FirstOrDefaultAsync(u => u.Id.ToString() == userId && u.AppId == app.Id);
            if (dappUser?.WalletPublicKey == null)
            {
                await Clients.Caller.SendAsync("Error", "No wallet connected");
                return;
            }

            // Add client to group for user-specific updates
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);

            // Start streaming
            await StreamTransactions(dappUser.WalletPublicKey, userId, filter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to transactions for {UserId}", userId);
            await Clients.Caller.SendAsync("Error", "Subscription failed");
        }
    }

    private async Task StreamTransactions(string walletPublicKey, string userId, TransactionFilter filter)
    {
        var subscriptionId = await _solanaService.SubscribeToWalletTransactionsAsync(
            walletPublicKey,
            async update =>
            {
                if (ShouldIncludeTransaction(update, filter))
                {
                    await Clients.Group(userId).SendAsync("TransactionUpdate", update);

                    // Aggregate stats
                    var stats = await UpdateStats(userId, update);
                    await Clients.Group(userId).SendAsync("StatsUpdate", stats);

                    // Todo Send email notification
                    {/*
                    // Notify high-value transactions
                    if (update.Amount > 1) // e.g., >1 SOL
                    {
                        var user = await _dbContext.DAppUsers.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
                        if (user?.Email != null)
                        {
                            await _emailService.SendVerificationEmailAsync(
                                user.Email,
                                "High-Value Transaction Alert",
                                $"Transaction {update.Signature} processed {update.Amount} SOL ({update.ActionType})."
                            );
                        }
                    }
                    }
                }
            }
        );

        // Handle disconnection
        Context.GetHttpContext()?.Response.OnCompleted(() =>
        {
            _solanaService.UnsubscribeFromWalletTransactions(subscriptionId);
            return Task.CompletedTask;
        });
    }

    private bool ShouldIncludeTransaction(TransactionUpdate update, TransactionFilter filter)
    {
        if (!string.IsNullOrEmpty(filter.ActionType) && update.ActionType != filter.ActionType)
            return false;
        if (!string.IsNullOrEmpty(filter.TokenMint) && update.TokenMint != filter.TokenMint)
            return false;
        return true;
    }

    private async Task<TransactionStats> UpdateStats(string userId, TransactionUpdate update)
    {
        var stats = await _dbContext.TransactionStats.FirstOrDefaultAsync(s => s.UserId == userId)
            ?? new TransactionStatsEntity { UserId = userId };

        stats.TotalAmount += update.Amount;
        stats.TransactionCount++;
        stats.ActionTypeCounts[update.ActionType] = stats.ActionTypeCounts.GetValueOrDefault(update.ActionType, 0) + 1;

        _dbContext.TransactionStats.Update(stats);
        await _dbContext.SaveChangesAsync();

        return new TransactionStats
        {
            TotalAmount = stats.TotalAmount,
            TransactionCount = stats.TransactionCount,
            ActionTypeCounts = stats.ActionTypeCounts
                .Select(kvp => new ActionTypeCount
                {
                    ActionType = kvp.Key,
                    Count = kvp.Value
                })
                .ToList()
        };
    }
}

// Data/TransactionStatsEntity.cs
**/