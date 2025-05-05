namespace Prismon.Api.Models;
public class TransactionUpdate
{
    public string Signature { get; set; } = string.Empty;
    public string WalletPublicKey { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty; // e.g., Transfer, Swap, StoreBlob
    public decimal Amount { get; set; } // In SOL or tokens
    public string TokenMint { get; set; } = string.Empty; // If applicable
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = string.Empty; // Pending, Confirmed, Failed
    public string? Details { get; set; } // e.g., "Raydium swap SOL->USDC"
}