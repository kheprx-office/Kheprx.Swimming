using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Health.Infrastructure.Repositories;

internal sealed class MedicalTestRepository : IMedicalTestRepository
{
    private readonly HealthDbContext _db;
    public MedicalTestRepository(HealthDbContext db) => _db = db;

    public async Task<IReadOnlyList<MedicalTest>> ListAsync(CancellationToken ct = default)
        => await _db.MedicalTests.AsNoTracking().OrderBy(t => t.NameEn).ToListAsync(ct);

    public async Task AddAsync(MedicalTest test, CancellationToken ct = default)
        => await _db.MedicalTests.AddAsync(test, ct);

    public Task<MedicalTest?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.MedicalTests.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task RemoveAsync(MedicalTest test, CancellationToken ct = default)
    {
        _db.MedicalTests.Remove(test);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
