namespace GoldenGuard.Domain.DTOs;

public sealed class CreateUserDto
{
    public string Name { get; init; } = default!;
    public string Email { get; init; } = default!;
    public decimal MonthlyIncome { get; init; }
}
