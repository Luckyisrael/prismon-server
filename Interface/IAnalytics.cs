using Prismon.Api.DTOs;

namespace Prismon.Api.Interface;

public interface IAnalyticsService
{
    Task<UserActivityDto> GetUserActivityAsync(Guid appId, string developerId);
    Task<List<TransactionDto>> GetUserTransactionsAsync(string userId, Guid appId);
}