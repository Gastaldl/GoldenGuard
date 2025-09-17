using GoldenGuard.Domain.Entities;

namespace GoldenGuard.Infrastructure.Repositories;

public interface ITransactionRepository
{
    Task<long> CreateAsync(Transaction tx);
    Task<Transaction?> GetAsync(long id);
    Task<IEnumerable<Transaction>> ListByUserAsync(long userId, DateTime? from = null, DateTime? to = null);
    Task<bool> UpdateAsync(long id, Transaction tx);
    Task<bool> DeleteAsync(long id);
}
