using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Health.Infrastructure.Repositories;

internal sealed class InBodyReadingRepository : IInBodyReadingRepository
{
    private readonly HealthDbContext _db;
    public InBodyReadingRepository(HealthDbContext db) => _db = db;

    public async Task<IReadOnlyList<InBodyReading>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default)
        => await _db.InBodyReadings.AsNoTracking()
              .Where(r => r.SwimmerId == swimmerId)
              .OrderByDescending(r => r.ReadingDate).ThenByDescending(r => r.CreatedAt)
              .ToListAsync(ct);

    public async Task AddAsync(InBodyReading reading, CancellationToken ct = default)
        => await _db.InBodyReadings.AddAsync(reading, ct);

    public Task<InBodyReading?> GetTrackedAsync(Guid readingId, CancellationToken ct = default)
        => _db.InBodyReadings.FirstOrDefaultAsync(r => r.Id == readingId, ct);

    public void Remove(InBodyReading reading) => _db.InBodyReadings.Remove(reading);

    public async Task SaveChangesAsync(CancellationToken ct = default) => await _db.SaveChangesAsync(ct);
}
