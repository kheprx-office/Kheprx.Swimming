namespace Kheprx.BaseBackend.Identity.Contracts;

public interface IIdentityModule
{
    Task<bool> UserExistsAsync(Guid userId, CancellationToken ct = default);

    // Person rows for the given worker user-ids, filtered by an optional name/NID search and
    // active flag. Empty input → empty result.
    Task<IReadOnlyList<WorkerPersonDto>> ListWorkersAsync(
        IReadOnlyList<Guid> workerIds, string? search, bool? isActive, CancellationToken ct = default);

    // Single worker's person fields + HireDate for the detail header. Null if the id isn't a worker.
    Task<WorkerDetailPersonDto?> GetWorkerDetailAsync(Guid workerId, CancellationToken ct = default);

    // Manager id → display name for the given manager ids (managers joined with users). Empty input → empty result.
    Task<IReadOnlyList<ManagerNameDto>> ListManagerNamesAsync(
        IReadOnlyList<Guid> managerIds, CancellationToken ct = default);

    // All managers (id + display name) for manager dropdowns.
    Task<IReadOnlyList<ManagerNameDto>> ListManagersAsync(CancellationToken ct = default);

    // A manager's current monthly salary (server-side snapshot source), or null if not a manager.
    Task<decimal?> GetManagerMonthlySalaryAsync(Guid managerId, CancellationToken ct = default);

    // Base daily wages for the given worker user-ids (workers joined with users on UserId), a per-assignment
    // snapshot source. Ids without a worker row are absent. Empty input → empty result.
    Task<IReadOnlyDictionary<Guid, decimal>> GetWorkerDailyWagesAsync(
        IReadOnlyList<Guid> workerUserIds, CancellationToken ct = default);

    // Base daily wages for the given moqawel user-ids (the moqawel analogue of the above).
    // Ids without a moqawel row are absent. Empty input → empty result.
    Task<IReadOnlyDictionary<Guid, decimal>> GetMoqawelDailyWagesAsync(
        IReadOnlyList<Guid> moqawelUserIds, CancellationToken ct = default);

    // Moqawel (contractor) person rows for the given moqawel ids, filtered by an optional
    // name/NID search and active flag. Empty input → empty result.
    Task<IReadOnlyList<MoqawelPersonDto>> ListMoqaweleenAsync(
        IReadOnlyList<Guid> moqawelIds, string? search, bool? isActive, CancellationToken ct = default);

    // All active moqawel-users (id = users.Id + name) for contractor pickers.
    Task<IReadOnlyList<PersonOptionDto>> ListAvailableMoqaweleenAsync(CancellationToken ct = default);

    // All active worker-users (id = users.Id + name) for worker pickers.
    Task<IReadOnlyList<PersonOptionDto>> ListAvailableWorkersAsync(CancellationToken ct = default);

    // A manager profile's linked user-account id (managers.UserId), or null if the id isn't a manager.
    Task<Guid?> GetManagerUserIdAsync(Guid managerId, CancellationToken ct = default);

    // User id → display name (users.FullName) for the given user ids. Empty input → empty result.
    Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(
        IReadOnlyList<Guid> userIds, CancellationToken ct = default);
}
