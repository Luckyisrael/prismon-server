namespace Prismon.Api.Models;

public class TransactionStatsEntity
{
    public string UserId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int TransactionCount { get; set; }
    public Dictionary<string, int> ActionTypeCounts { get; set; } = new();
}
