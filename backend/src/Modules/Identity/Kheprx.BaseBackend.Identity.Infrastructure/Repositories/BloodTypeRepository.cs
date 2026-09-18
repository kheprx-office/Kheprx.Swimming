using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class BloodTypeRepository : IBloodTypeRepository
{
    private readonly IdentityDbContext _db;
    public BloodTypeRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<BloodType>> GetAllAsync(CancellationToken ct = default)
        => await _db.BloodTypes.AsNoTracking().OrderBy(b => b.Code).ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _db.BloodTypes.AsNoTracking().AnyAsync(b => b.Id == id, ct);
}
