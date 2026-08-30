using Kheprx.BaseBackend.Identity.Domain.ReadModels;

namespace Kheprx.BaseBackend.Identity.Domain.Repositories;

public interface IManagerReadRepository
{
    // Manager id → display name (managers ⨝ users) for the given manager ids.
    // Empty input → empty result (no query).
    Task<IReadOnlyList<ManagerNameRow>> ListNamesAsync(IReadOnlyList<Guid> managerIds, CancellationToken ct = default);

    // All managers (managers ⨝ users) → id + display name, ordered by name.
    Task<IReadOnlyList<ManagerNameRow>> ListAllAsync(CancellationToken ct = default);

    // A single manager's current monthly salary, or null when the id is not a manager.
    Task<decimal?> GetMonthlySalaryAsync(Guid managerId, CancellationToken ct = default);
}
