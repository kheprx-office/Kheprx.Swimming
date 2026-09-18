using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class ObservationCategoryRepository : IObservationCategoryRepository
{
    private readonly IdentityDbContext _db;
    public ObservationCategoryRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<ObservationCategory>> GetAllAsync(CancellationToken ct = default)
        => await _db.ObservationCategories.AsNoTracking().OrderBy(c => c.Code).ToListAsync(ct);
}
