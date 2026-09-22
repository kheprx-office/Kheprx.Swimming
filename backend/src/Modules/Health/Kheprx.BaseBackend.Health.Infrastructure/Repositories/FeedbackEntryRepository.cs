using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Health.Infrastructure.Repositories;

internal sealed class FeedbackEntryRepository : IFeedbackEntryRepository
{
    private readonly HealthDbContext _db;
    public FeedbackEntryRepository(HealthDbContext db) => _db = db;

    public async Task<IReadOnlyList<FeedbackEntry>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default)
        => await _db.FeedbackEntries.AsNoTracking()
              .Where(e => e.SwimmerId == swimmerId)
              .OrderByDescending(e => e.EntryDate).ThenByDescending(e => e.Id)
              .ToListAsync(ct);

    public async Task AddAsync(FeedbackEntry entry, CancellationToken ct = default)
        => await _db.FeedbackEntries.AddAsync(entry, ct);

    public Task<FeedbackEntry?> GetTrackedAsync(Guid entryId, CancellationToken ct = default)
        => _db.FeedbackEntries.FirstOrDefaultAsync(e => e.Id == entryId, ct);

    public void Remove(FeedbackEntry entry) => _db.FeedbackEntries.Remove(entry);

    public async Task SaveChangesAsync(CancellationToken ct = default) => await _db.SaveChangesAsync(ct);
}
