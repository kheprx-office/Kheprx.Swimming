using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Repositories;

internal sealed class ChampionshipEnrollmentRepository : IChampionshipEnrollmentRepository
{
    private readonly ChampionshipsDbContext _db;
    public ChampionshipEnrollmentRepository(ChampionshipsDbContext db) => _db = db;

    public async Task<IReadOnlyList<Guid>> ListSwimmerIdsAsync(Guid eventId, CancellationToken ct = default)
        => await _db.Enrollments.AsNoTracking()
              .Where(e => e.EventId == eventId)
              .Select(e => e.SwimmerId)
              .ToListAsync(ct);

    public async Task ReplaceAsync(Guid eventId, IReadOnlyList<Guid> swimmerIds, CancellationToken ct = default)
    {
        var existing = await _db.Enrollments.Where(e => e.EventId == eventId).ToListAsync(ct);
        _db.Enrollments.RemoveRange(existing);

        var distinct = swimmerIds.Distinct();
        foreach (var swimmerId in distinct)
            _db.Enrollments.Add(new ChampionshipEnrollment(eventId, swimmerId));

        await _db.SaveChangesAsync(ct);
    }
}
