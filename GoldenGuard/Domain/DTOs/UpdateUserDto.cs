namespace GoldenGuard.Domain.DTOs;

public sealed class UpdateUserDto
{
    public string? Name { get; init; }
    public string? Email { get; init; }
    public decimal? MonthlyIncome { get; init; }
}
