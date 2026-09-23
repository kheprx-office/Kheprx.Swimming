using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Repositories;

internal sealed class CompetitionEventRepository : ICompetitionEventRepository
{
    private readonly ChampionshipsDbContext _db;
    public CompetitionEventRepository(ChampionshipsDbContext db) => _db = db;

    public async Task<IReadOnlyList<CompetitionEvent>> ListAsync(CancellationToken ct = default)
        => await _db.CompetitionEvents.AsNoTracking()
              .OrderByDescending(e => e.StartDate).ThenByDescending(e => e.Id)
              .ToListAsync(ct);

    public async Task AddAsync(CompetitionEvent competitionEvent, CancellationToken ct = default)
    {
        _db.CompetitionEvents.Add(competitionEvent);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<CompetitionEvent?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.CompetitionEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
}
