using GoldenGuard.Data;
using GoldenGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoldenGuard.Infrastructure.Repositories;

public sealed class EfTransactionRepository(GgDbContext db) : ITransactionRepository
{
    public async Task<long> CreateAsync(Transaction tx)
    {
        db.Transactions.Add(tx);
        await db.SaveChangesAsync();
        return tx.Id;
    }

    public Task<Transaction?> GetAsync(long id) =>
        db.Transactions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);

    public async Task<IEnumerable<Transaction>> ListByUserAsync(long userId, DateTime? from = null, DateTime? to = null)
    {
        var q = db.Transactions.AsNoTracking().Where(t => t.UserId == userId);
        if (from.HasValue) q = q.Where(t => t.OccurredAt >= from.Value);
        if (to.HasValue) q = q.Where(t => t.OccurredAt < to.Value);
        return await q.OrderByDescending(t => t.OccurredAt).ToListAsync();
    }

    public async Task<bool> UpdateAsync(long id, Transaction tx)
    {
        var curr = await db.Transactions.FirstOrDefaultAsync(x => x.Id == id);
        if (curr is null) return false;
        curr.Operator = tx.Operator;
        curr.Kind = tx.Kind;
        curr.Amount = tx.Amount;
        curr.OccurredAt = tx.OccurredAt;
        curr.RawLabel = tx.RawLabel;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var curr = await db.Transactions.FirstOrDefaultAsync(x => x.Id == id);
        if (curr is null) return false;
        db.Transactions.Remove(curr);
        await db.SaveChangesAsync();
        return true;
    }
}