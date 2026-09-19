using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class FitnessAssessmentRepository : IFitnessAssessmentRepository
{
    private readonly IdentityDbContext _db;
    public FitnessAssessmentRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<FitnessAssessment>> GetAllAsync(CancellationToken ct = default)
        => await _db.FitnessAssessments.AsNoTracking().OrderBy(f => f.NameEn).ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _db.FitnessAssessments.AsNoTracking().AnyAsync(f => f.Id == id, ct);
}
