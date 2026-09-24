using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Championships.Infrastructure.Repositories;

internal sealed class CompetitionScheduleRepository : ICompetitionScheduleRepository
{
    private readonly ChampionshipsDbContext _db;
    public CompetitionScheduleRepository(ChampionshipsDbContext db) => _db = db;

    public async Task<IReadOnlyList<ScheduleDayRow>> GetAsync(Guid eventId, CancellationToken ct = default)
    {
        var days = await _db.CompetitionDays.AsNoTracking()
            .Where(d => d.EventId == eventId)
            .OrderBy(d => d.DayDate).ThenBy(d => d.Id).ToListAsync(ct);
        var dayIds = days.Select(d => d.Id).ToList();

        var sessions = await _db.RaceSessions.AsNoTracking()
            .Where(s => dayIds.Contains(s.DayId)).ToListAsync(ct);
        var sessionIds = sessions.Select(s => s.Id).ToList();

        var assignments = await _db.RaceAssignments.AsNoTracking()
            .Where(a => sessionIds.Contains(a.RaceSessionId)).ToListAsync(ct);

        var swimmersBySession = assignments
            .GroupBy(a => a.RaceSessionId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(a => a.SwimmerId).ToList());
        var sessionsByDay = sessions
            .GroupBy(s => s.DayId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return days.Select(d => new ScheduleDayRow(
            d.Id, d.LabelEn, d.LabelAr, d.DayDate,
            (sessionsByDay.TryGetValue(d.Id, out var ss) ? ss : new List<RaceSession>())
                .OrderBy(s => s.ScheduledTime ?? TimeOnly.MinValue).ThenBy(s => s.Id)
                .Select(s => new ScheduleRaceRow(
                    s.Id, s.StrokeId, s.DistanceId, s.ScheduledTime,
                    swimmersBySession.TryGetValue(s.Id, out var sw) ? sw : new List<Guid>()))
                .ToList()))
            .ToList();
    }

    public async Task ReplaceAsync(Guid eventId, IReadOnlyList<ScheduleDayInput> days, CancellationToken ct = default)
    {
        var oldDays = await _db.CompetitionDays.Where(d => d.EventId == eventId).ToListAsync(ct);
        var oldDayIds = oldDays.Select(d => d.Id).ToList();
        var oldSessions = await _db.RaceSessions.Where(s => oldDayIds.Contains(s.DayId)).ToListAsync(ct);
        var oldSessionIds = oldSessions.Select(s => s.Id).ToList();
        var oldAssignments = await _db.RaceAssignments.Where(a => oldSessionIds.Contains(a.RaceSessionId)).ToListAsync(ct);

        _db.RaceAssignments.RemoveRange(oldAssignments);
        _db.RaceSessions.RemoveRange(oldSessions);
        _db.CompetitionDays.RemoveRange(oldDays);

        foreach (var d in days)
        {
            var day = new CompetitionDay(eventId, d.LabelEn, d.LabelAr, d.DayDate);
            _db.CompetitionDays.Add(day);
            foreach (var r in d.Races)
            {
                var session = new RaceSession(day.Id, r.StrokeId, r.DistanceId, r.ScheduledTime);
                _db.RaceSessions.Add(session);
                foreach (var swimmerId in r.SwimmerIds.Distinct())
                    _db.RaceAssignments.Add(new RaceAssignment(session.Id, swimmerId));
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
