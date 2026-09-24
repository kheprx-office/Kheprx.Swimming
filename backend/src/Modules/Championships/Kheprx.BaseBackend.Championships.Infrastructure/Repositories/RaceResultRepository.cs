using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Repositories;

internal sealed class RaceResultRepository : IRaceResultRepository
{
    private readonly ChampionshipsDbContext _db;
    public RaceResultRepository(ChampionshipsDbContext db) => _db = db;

    public async Task<IReadOnlyList<RaceResultRow>> GetByEventAsync(Guid eventId, CancellationToken ct = default)
    {
        var dayIds = await _db.CompetitionDays.AsNoTracking()
            .Where(d => d.EventId == eventId).Select(d => d.Id).ToListAsync(ct);
        var sessionIds = await _db.RaceSessions.AsNoTracking()
            .Where(s => dayIds.Contains(s.DayId)).Select(s => s.Id).ToListAsync(ct);
        var rows = await _db.RaceResults.AsNoTracking()
            .Where(r => sessionIds.Contains(r.RaceSessionId)).ToListAsync(ct);

        return rows.Select(r => new RaceResultRow(
            r.Id, r.RaceSessionId, r.SwimmerId, r.TimeMs, r.Points, r.IsPersonalBest, r.RecordedBy)).ToList();
    }

    public async Task ReplaceForSessionAsync(Guid raceSessionId, IReadOnlyList<RaceResultInput> rows, CancellationToken ct = default)
    {
        var old = await _db.RaceResults.Where(r => r.RaceSessionId == raceSessionId).ToListAsync(ct);
        _db.RaceResults.RemoveRange(old);
        foreach (var r in rows)
            _db.RaceResults.Add(new RaceResult(raceSessionId, r.SwimmerId, r.TimeMs, r.Points, r.IsPersonalBest, r.RecordedBy));
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetBestTimesAsync(Guid distanceId, Guid strokeId, Guid excludeSessionId, IReadOnlyList<Guid> swimmerIds, CancellationToken ct = default)
    {
        if (swimmerIds.Count == 0) return new Dictionary<Guid, int>();

        var sessionIds = await _db.RaceSessions.AsNoTracking()
            .Where(s => s.DistanceId == distanceId && s.StrokeId == strokeId && s.Id != excludeSessionId)
            .Select(s => s.Id).ToListAsync(ct);
        if (sessionIds.Count == 0) return new Dictionary<Guid, int>();

        var rows = await _db.RaceResults.AsNoTracking()
            .Where(r => sessionIds.Contains(r.RaceSessionId) && swimmerIds.Contains(r.SwimmerId))
            .ToListAsync(ct);

        return rows.GroupBy(r => r.SwimmerId).ToDictionary(g => g.Key, g => g.Min(r => r.TimeMs));
    }

    public async Task<IReadOnlyList<SwimmerRaceLineRow>> GetSwimmerRaceLinesAsync(Guid swimmerId, CancellationToken ct = default)
    {
        var query =
            from r in _db.RaceResults.AsNoTracking().Where(x => x.SwimmerId == swimmerId)
            join s in _db.RaceSessions.AsNoTracking() on r.RaceSessionId equals s.Id
            join d in _db.CompetitionDays.AsNoTracking() on s.DayId equals d.Id
            select new SwimmerRaceLineRow(
                d.EventId, d.LabelEn, d.LabelAr, d.DayDate,
                s.ScheduledTime, s.DistanceId, s.StrokeId, r.TimeMs, r.IsPersonalBest);

        return await query.ToListAsync(ct);
    }
}
