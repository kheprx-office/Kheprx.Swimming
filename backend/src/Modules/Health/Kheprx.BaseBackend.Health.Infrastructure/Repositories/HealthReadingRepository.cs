using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Health.Infrastructure.Repositories;

internal sealed class HealthReadingRepository : IHealthReadingRepository
{
    private readonly HealthDbContext _db;
    public HealthReadingRepository(HealthDbContext db) => _db = db;

    public async Task AddAsync(HealthReading reading, CancellationToken ct = default)
        => await _db.HealthReadings.AddAsync(reading, ct);

    public async Task<IReadOnlyList<HealthReading>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default)
        => await _db.HealthReadings.AsNoTracking()
              .Where(r => r.SwimmerId == swimmerId)
              .OrderByDescending(r => r.ReadingDate)
              .ThenByDescending(r => r.Id)
              .ToListAsync(ct);

    public Task<HealthReading?> GetTrackedAsync(Guid id, CancellationToken ct = default)
        => _db.HealthReadings.FirstOrDefaultAsync(r => r.Id == id, ct);

    public void Remove(HealthReading reading) => _db.HealthReadings.Remove(reading);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
