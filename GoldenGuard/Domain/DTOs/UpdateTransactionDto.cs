namespace GoldenGuard.Domain.DTOs;

public sealed class UpdateTransactionDto
{
    public string? Operator { get; init; }
    public string? Kind { get; init; }
    public decimal? Amount { get; init; }
    public DateTime? OccurredAt { get; init; }
    public string? RawLabel { get; init; }
}
