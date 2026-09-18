using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Kheprx.BaseBackend.Health.Infrastructure.Data;

namespace Kheprx.BaseBackend.Health.Infrastructure.Repositories;

internal sealed class ObservationRepository : IObservationRepository
{
    private readonly HealthDbContext _db;
    public ObservationRepository(HealthDbContext db) => _db = db;

    public async Task AddAsync(Observation observation, CancellationToken ct = default)
        => await _db.Observations.AddAsync(observation, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
