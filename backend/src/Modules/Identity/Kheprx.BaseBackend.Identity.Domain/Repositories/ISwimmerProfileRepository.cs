using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.ReadModels;

namespace Kheprx.BaseBackend.Identity.Domain.Repositories;

public interface ISwimmerProfileRepository
{
    Task<int> CountAsync(CancellationToken ct = default);
    Task<int> GetMaxUidNumberAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SwimmerListRow>> ListAsync(string? search = null, CancellationToken ct = default);
    Task AddAsync(SwimmerProfile profile, CancellationToken ct = default);
    Task AddSpecializationsAsync(IEnumerable<SwimmerSpecialization> specializations, CancellationToken ct = default);
    Task<bool> SaveChangesAsync(CancellationToken ct = default);  // true = saved; false = unique-index conflict
}
