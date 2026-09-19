using Kheprx.BaseBackend.Health.Domain.Entities;

namespace Kheprx.BaseBackend.Health.Domain.Repositories;

public interface IInBodyReadingRepository
{
    Task<IReadOnlyList<InBodyReading>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default);
    Task AddAsync(InBodyReading reading, CancellationToken ct = default);
    Task<InBodyReading?> GetTrackedAsync(Guid readingId, CancellationToken ct = default);
    void Remove(InBodyReading reading);
    Task SaveChangesAsync(CancellationToken ct = default);
}
