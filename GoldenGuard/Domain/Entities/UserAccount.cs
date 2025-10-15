namespace GoldenGuard.Domain.Entities;

public sealed class UserAccount
{
    public long Id { get; set; }
    public long UserId { get; set; }

    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!; // em dev está texto puro
    public string Role { get; set; } = null!;         // "admin" | "user"

    public DateTime CreatedAt { get; set; }
}