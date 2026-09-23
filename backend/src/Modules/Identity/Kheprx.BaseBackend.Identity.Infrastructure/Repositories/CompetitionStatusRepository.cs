using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class CompetitionStatusRepository : ICompetitionStatusRepository
{
    private readonly IdentityDbContext _db;
    public CompetitionStatusRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<CompetitionStatus>> GetAllAsync(CancellationToken ct = default)
        => await _db.CompetitionStatuses.AsNoTracking().OrderBy(s => s.Code).ToListAsync(ct);
}
