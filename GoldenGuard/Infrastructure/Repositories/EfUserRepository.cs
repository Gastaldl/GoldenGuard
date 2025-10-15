using GoldenGuard.Data;
using GoldenGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoldenGuard.Infrastructure.Repositories;

public sealed class EfUserRepository(GgDbContext db) : IUserRepository
{
    public async Task<long> CreateAsync(UserProfile user)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    public Task<UserProfile?> GetAsync(long id) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);

    public async Task<IEnumerable<UserProfile>> ListAsync() =>
        await db.Users.AsNoTracking().OrderByDescending(x => x.Id).ToListAsync();

    public async Task<bool> UpdateAsync(long id, UserProfile user)
    {
        var curr = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (curr is null) return false;
        curr.Name = user.Name;
        curr.Email = user.Email;
        curr.MonthlyIncome = user.MonthlyIncome;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var curr = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (curr is null) return false;
        db.Users.Remove(curr);
        await db.SaveChangesAsync();
        return true;
    }
}