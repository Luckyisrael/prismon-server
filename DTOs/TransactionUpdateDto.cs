namespace Prismon.Api.DTOs;

public class TransactionUpdateDto
{
    public string Signature { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty; // In SOL or lamports
    public DateTime? Timestamp { get; set; }
}