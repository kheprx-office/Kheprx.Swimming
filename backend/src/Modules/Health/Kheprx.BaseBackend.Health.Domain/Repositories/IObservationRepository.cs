using Kheprx.BaseBackend.Health.Domain.Entities;

namespace Kheprx.BaseBackend.Health.Domain.Repositories;

public interface IObservationRepository
{
    Task AddAsync(Observation observation, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
