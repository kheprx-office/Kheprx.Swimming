using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class StrokeRepository : IStrokeRepository
{
    private readonly IdentityDbContext _db;
    public StrokeRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<Stroke>> GetAllAsync(CancellationToken ct = default)
        => await _db.Strokes.AsNoTracking().OrderBy(s => s.NameEn).ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _db.Strokes.AsNoTracking().AnyAsync(s => s.Id == id, ct);
}
