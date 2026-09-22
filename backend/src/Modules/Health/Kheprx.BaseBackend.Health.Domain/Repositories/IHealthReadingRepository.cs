using Kheprx.BaseBackend.Health.Domain.Entities;

namespace Kheprx.BaseBackend.Health.Domain.Repositories;

public interface IHealthReadingRepository
{
    Task AddAsync(HealthReading reading, CancellationToken ct = default);
    Task<IReadOnlyList<HealthReading>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default);
    Task<HealthReading?> GetTrackedAsync(Guid id, CancellationToken ct = default);
    void Remove(HealthReading reading);
    Task SaveChangesAsync(CancellationToken ct = default);
}
