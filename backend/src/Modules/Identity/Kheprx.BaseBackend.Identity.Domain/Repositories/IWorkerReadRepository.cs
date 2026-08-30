using Kheprx.BaseBackend.Identity.Domain.ReadModels;

namespace Kheprx.BaseBackend.Identity.Domain.Repositories;

public interface IWorkerReadRepository
{
    // Returns worker rows for the given worker ids, filtered by an optional name/NID
    // search and active flag. Empty input → empty result (no query).
    Task<IReadOnlyList<WorkerListRow>> ListAsync(
        IReadOnlyList<Guid> workerIds, string? search, bool? isActive, CancellationToken ct = default);

    // Single worker (by users.Id) with person fields + HireDate, or null if not a worker.
    Task<WorkerDetailRow?> GetDetailAsync(Guid workerUserId, CancellationToken ct = default);

    // All worker-users (workers ⨝ users) → rows whose Id is users.Id, ordered by name.
    // Optional active filter. Feeds the create-project workers picker.
    Task<IReadOnlyList<WorkerListRow>> ListAllAsync(bool? isActive, CancellationToken ct = default);
}
