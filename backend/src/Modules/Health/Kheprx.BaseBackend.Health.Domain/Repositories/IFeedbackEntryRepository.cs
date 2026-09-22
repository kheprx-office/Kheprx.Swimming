using Kheprx.BaseBackend.Health.Domain.Entities;

namespace Kheprx.BaseBackend.Health.Domain.Repositories;

public interface IFeedbackEntryRepository
{
    Task<IReadOnlyList<FeedbackEntry>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default);
    Task AddAsync(FeedbackEntry entry, CancellationToken ct = default);
    Task<FeedbackEntry?> GetTrackedAsync(Guid entryId, CancellationToken ct = default);
    void Remove(FeedbackEntry entry);
    Task SaveChangesAsync(CancellationToken ct = default);
}
