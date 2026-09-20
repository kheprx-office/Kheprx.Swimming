using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Health.Infrastructure.Repositories;

internal sealed class ObservationRepository : IObservationRepository
{
    private readonly HealthDbContext _db;
    public ObservationRepository(HealthDbContext db) => _db = db;

    public async Task<IReadOnlyList<Observation>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default)
        => await _db.Observations.AsNoTracking()
              .Where(o => o.SwimmerId == swimmerId)
              .OrderByDescending(o => o.ObservedDate)
              .ToListAsync(ct);

    public Task<Observation?> GetTrackedAsync(Guid id, CancellationToken ct = default)
        => _db.Observations.FirstOrDefaultAsync(o => o.Id == id, ct);

    public void Remove(Observation observation) => _db.Observations.Remove(observation);

    public async Task AddAsync(Observation observation, CancellationToken ct = default)
        => await _db.Observations.AddAsync(observation, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
