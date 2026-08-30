using Kheprx.BaseBackend.Identity.Domain.Entities;

namespace Kheprx.BaseBackend.Identity.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByNidAsync(string nid, CancellationToken ct = default);
    Task<IReadOnlyList<User>> ListAsync(string? search = null, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListCodesByPrefixAsync(string prefix, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
