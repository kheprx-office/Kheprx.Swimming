using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class ClubRepository : IClubRepository
{
    private readonly IdentityDbContext _db;
    public ClubRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<Club>> GetAllAsync(CancellationToken ct = default)
        => await _db.Clubs.AsNoTracking().OrderBy(c => c.NameEn).ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _db.Clubs.AsNoTracking().AnyAsync(c => c.Id == id, ct);
}
