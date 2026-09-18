using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class GenderRepository : IGenderRepository
{
    private readonly IdentityDbContext _db;
    public GenderRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<Gender>> GetAllAsync(CancellationToken ct = default)
        => await _db.Genders.AsNoTracking().OrderBy(g => g.NameEn).ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _db.Genders.AsNoTracking().AnyAsync(g => g.Id == id, ct);
}
