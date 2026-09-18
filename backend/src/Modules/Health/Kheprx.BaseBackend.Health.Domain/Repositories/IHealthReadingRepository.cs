using Kheprx.BaseBackend.Health.Domain.Entities;

namespace Kheprx.BaseBackend.Health.Domain.Repositories;

public interface IHealthReadingRepository
{
    Task AddAsync(HealthReading reading, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
