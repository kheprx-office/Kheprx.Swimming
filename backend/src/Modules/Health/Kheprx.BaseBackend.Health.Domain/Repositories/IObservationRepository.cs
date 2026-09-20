using Kheprx.BaseBackend.Health.Domain.Entities;

namespace Kheprx.BaseBackend.Health.Domain.Repositories;

public interface IObservationRepository
{
    Task<IReadOnlyList<Observation>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default);
    Task AddAsync(Observation observation, CancellationToken ct = default);
    Task<Observation?> GetTrackedAsync(Guid id, CancellationToken ct = default);
    void Remove(Observation observation);
    Task SaveChangesAsync(CancellationToken ct = default);
}
