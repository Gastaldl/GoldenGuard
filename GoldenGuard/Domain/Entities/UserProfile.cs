namespace GoldenGuard.Domain.Entities;

public sealed class UserProfile
{
    public long Id { get; init; }
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public decimal MonthlyIncome { get; set; }
    public DateTime CreatedAt { get; init; }
}
