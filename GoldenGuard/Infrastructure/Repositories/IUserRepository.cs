using GoldenGuard.Domain.Entities;

namespace GoldenGuard.Infrastructure.Repositories;

public interface IUserRepository
{
    Task<long> CreateAsync(UserProfile user);
    Task<UserProfile?> GetAsync(long id);
    Task<IEnumerable<UserProfile>> ListAsync();
    Task<bool> UpdateAsync(long id, UserProfile user);
    Task<bool> DeleteAsync(long id);
}
