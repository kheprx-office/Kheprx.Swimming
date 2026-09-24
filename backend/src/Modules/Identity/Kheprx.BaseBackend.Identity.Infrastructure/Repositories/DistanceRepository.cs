using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class DistanceRepository : IDistanceRepository
{
    private readonly IdentityDbContext _db;
    public DistanceRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<Distance>> GetAllAsync(CancellationToken ct = default)
        => await _db.Distances.AsNoTracking().OrderBy(d => d.Meters).ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _db.Distances.AsNoTracking().AnyAsync(d => d.Id == id, ct);
}
