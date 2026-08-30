using Kheprx.BaseBackend.Identity.Domain.Entities;

namespace Kheprx.BaseBackend.Identity.Domain.Repositories;

public interface IUserProfileRepository
{
    Task<Manager?> GetManagerAsync(Guid userId, CancellationToken ct = default);
    Task<Moqawel?> GetMoqawelAsync(Guid userId, CancellationToken ct = default);
    Task<Worker?> GetWorkerAsync(Guid userId, CancellationToken ct = default);
    Task AddManagerAsync(Manager manager, CancellationToken ct = default);
    Task AddMoqawelAsync(Moqawel moqawel, CancellationToken ct = default);
    Task AddWorkerAsync(Worker worker, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, Manager>> ListManagersAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, Moqawel>> ListMoqaweleenAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, Worker>> ListWorkersAsync(CancellationToken ct = default);
}
