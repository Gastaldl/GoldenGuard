namespace GoldenGuard.Domain.Entities;

public sealed class Transaction
{
    public long Id { get; init; }
    public long UserId { get; set; }
    public string Operator { get; set; } = default!; // ex.: "PIX *BET365"
    public string Kind { get; set; } = default!;     // "DEPOSIT" | "WITHDRAWAL" | "OTHER"
    public decimal Amount { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? RawLabel { get; set; }
    public DateTime CreatedAt { get; init; }
}
