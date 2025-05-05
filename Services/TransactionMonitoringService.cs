/**using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Prismon.Api.Data;
using Prismon.Api.DTOs;
using Prismon.Api.Hubs;
using Solnet.Rpc;
using Solnet.Rpc.Models;
using Solnet.Rpc.Types;
using Solnet.Programs;
using Prismon.Api.Interface;
using System.Collections.Concurrent;
using Solnet.Rpc.Core.Sockets;
namespace Prismon.Api.Services;

public class TransactionMonitoringService : ITransactionMonitoringService, IDisposable
{
    private readonly PrismonDbContext _dbContext;
    private readonly IHubContext<TransactionHub> _hubContext;
    private readonly ILogger<TransactionMonitoringService> _logger;
    private readonly IRpcClient _rpcClient;
    private readonly ConcurrentDictionary<string, List<string>> _userConnections = new();
    private readonly ConcurrentDictionary<string, SubscriptionState> _subscriptions = new();
     private readonly IStreamingRpcClient _streamingRpcClient;

    public TransactionMonitoringService(
        PrismonDbContext dbContext,
        IHubContext<TransactionHub> hubContext,
        ILogger<TransactionMonitoringService> logger,
        IStreamingRpcClient streamingRpcClient)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
        _logger = logger;
        _rpcClient = ClientFactory.GetClient(Cluster.DevNet);
          _streamingRpcClient = streamingRpcClient;
    }
public async Task SubscribeToUserTransactionsAsync(string userId, Guid appId, string connectionId)
{
    var user = await _dbContext.DAppUsers
        .FirstOrDefaultAsync(u => u.Id.ToString() == userId && u.AppId == appId);
    if (user == null || string.IsNullOrEmpty(user.WalletPublicKey))
    {
        _logger.LogWarning("User {UserId} not found or no wallet connected for app {AppId}", userId, appId);
        await _hubContext.Clients.Client(connectionId).SendAsync("Error", "No wallet connected");
        return;
    }

    if (!_userConnections.TryGetValue(userId, out var connections))
    {
        connections = new List<string>();
        _userConnections[userId] = connections;
    }

    lock (connections)
    {
        if (!connections.Contains(connectionId))
        {
            connections.Add(connectionId);
        }
    }

    if (!_subscriptions.ContainsKey(userId))
    {
        var subscription = await _streamingRpcClient.SubscribeSignatureAsync(
            user.WalletPublicKey,
            async (sub, sig) =>
            {
                try 
                {
                    // First check if the subscription itself failed
                    if (sig.Error != null)
                    {
                        _logger.LogError("Subscription error for {Wallet}: {Error}", 
                            user.WalletPublicKey, sig.Error.Message);
                        return;
                    }

                    // Get the transaction details
                    var tx = await _rpcClient.GetTransactionAsync(sig.Context.Signature, Commitment.Confirmed);
                    if (!tx.WasSuccessful || tx.Result == null)
                    {
                        _logger.LogWarning("Failed to get transaction details for signature {Signature}", 
                            sig.Context.Signature);
                        return;
                    }

                    // Parse transaction amount
                    decimal amount = 0;
                    string currency = "SOL";
                    var message = tx.Result.Transaction.Message;
                    
                    // Check for SOL transfers
                    foreach (var instruction in message.Instructions)
                    {
                        if (instruction.ProgramId == SystemProgram.ProgramIdKey)
                        {
                            // For SystemProgram transfers, we can calculate the amount from balance changes
                            if (tx.Result.Meta != null)
                            {
                                // This is a simplified approach - you might need more sophisticated logic
                                // based on the transaction's pre/post balances
                                var balanceChange = tx.Result.Meta.PostBalances[0] - tx.Result.Meta.PreBalances[0];
                                amount = Convert.ToDecimal(balanceChange) / 1_000_000_000; // Convert lamports to SOL
                            }
                        }
                        // Add other program checks here (TokenProgram, etc.)
                    }

                    var update = new TransactionUpdateDto
                    {
                        Signature = sig.Context.Signature,
                        Amount = $"{amount} {currency}",
                        Timestamp = tx.Result.BlockTime.HasValue
                            ? DateTimeOffset.FromUnixTimeSeconds(tx.Result.BlockTime.Value).UtcDateTime
                            : null
                    };

                    if (_userConnections.TryGetValue(userId, out var userConnections))
                    {
                        await _hubContext.Clients.Clients(userConnections).SendAsync("TransactionUpdate", update);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing transaction for user {UserId}", userId);
                }
            },
            Commitment.Confirmed);

        _subscriptions[userId] = subscription;
        _logger.LogInformation("Subscribed to transactions for wallet {Wallet}", user.WalletPublicKey);
    }
}

    public async Task UnsubscribeFromUserTransactionsAsync(string userId, string connectionId)
    {
        if (_userConnections.TryGetValue(userId, out var connections))
        {
            lock (connections)
            {
                connections.Remove(connectionId);
                if (connections.Count == 0)
                {
                    _userConnections.TryRemove(userId, out _);
                    if (_subscriptions.TryRemove(userId, out var subscription))
                    {
                        subscription.UnsubscribeAsync().GetAwaiter().GetResult();
                        _logger.LogInformation("Unsubscribed from transactions for user {UserId}", userId);
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        foreach (var sub in _subscriptions.Values)
        {
            sub.UnsubscribeAsync().GetAwaiter().GetResult();
        }
        _subscriptions.Clear();
        _userConnections.Clear();
        GC.SuppressFinalize(this);
    }
}*/