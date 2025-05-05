public class TransactionStats
{
    public decimal TotalAmount { get; set; }
    public int TransactionCount { get; set; }
    public List<ActionTypeCount> ActionTypeCounts { get; set; } = new();
}

public class ActionTypeCount
{
    public int Id { get; set; }
    public string ActionType { get; set; }
    public int Count { get; set; }
    public int TransactionStatsId { get; set; } // Foreign key
    public TransactionStats TransactionStats { get; set; }
}