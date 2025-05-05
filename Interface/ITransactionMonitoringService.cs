namespace Prismon.Api.Interface;

public interface ITransactionMonitoringService
{
    Task SubscribeToUserTransactionsAsync(string userId, Guid appId, string connectionId);
    Task UnsubscribeFromUserTransactionsAsync(string userId, string connectionId);
}