using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Kheprx.BaseBackend.Health.Infrastructure.Data;

namespace Kheprx.BaseBackend.Health.Infrastructure.Repositories;

internal sealed class HealthReadingRepository : IHealthReadingRepository
{
    private readonly HealthDbContext _db;
    public HealthReadingRepository(HealthDbContext db) => _db = db;

    public async Task AddAsync(HealthReading reading, CancellationToken ct = default)
        => await _db.HealthReadings.AddAsync(reading, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
