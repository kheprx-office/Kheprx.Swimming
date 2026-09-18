using Kheprx.BaseBackend.Identity.Domain.Entities;

namespace Kheprx.BaseBackend.Identity.Domain.Repositories;

public interface IUserRepository
{
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AppUser>> ListAsync(string? search = null, CancellationToken ct = default);
    Task<string?> GetGenderCodeAsync(Guid? genderId, CancellationToken ct = default);
    Task AddAsync(AppUser user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
