namespace GoldenGuard.Domain.DTOs;

public sealed class CreateTransactionDto
{
    public long UserId { get; init; }
    public string Operator { get; init; } = default!;
    public string Kind { get; init; } = default!;
    public decimal Amount { get; init; }
    public DateTime OccurredAt { get; init; }
    public string? RawLabel { get; init; }
}
