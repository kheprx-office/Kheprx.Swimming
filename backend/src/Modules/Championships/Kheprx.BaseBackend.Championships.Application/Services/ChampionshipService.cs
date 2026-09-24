using Kheprx.BaseBackend.Championships.Application.DTOs;
using Kheprx.BaseBackend.Championships.Application.Services.Interfaces;
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;

namespace Kheprx.BaseBackend.Championships.Application.Services;

internal sealed class ChampionshipService : IChampionshipService
{
    private readonly ICompetitionEventRepository _events;
    private readonly IChampionshipEnrollmentRepository _enrollments;
    private readonly ICompetitionScheduleRepository _schedule;
    private readonly IRaceResultRepository _results;

    public ChampionshipService(ICompetitionEventRepository events, IChampionshipEnrollmentRepository enrollments, ICompetitionScheduleRepository schedule, IRaceResultRepository results)
    {
        _events = events;
        _enrollments = enrollments;
        _schedule = schedule;
        _results = results;
    }

    public async Task<IReadOnlyList<CompetitionEventDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await _events.ListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<CompetitionEventDto> CreateAsync(CreateCompetitionEventCommand c, CancellationToken ct = default)
    {
        var e = new CompetitionEvent(c.NameEn, c.NameAr, c.StartDate, c.EndDate, c.LocationEn, c.LocationAr, c.StatusId, c.CreatedBy);
        await _events.AddAsync(e, ct);
        return ToDto(e);
    }

    public async Task<CompetitionEventDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _events.GetByIdAsync(id, ct);
        return e is null ? null : ToDto(e);
    }

    public async Task<IReadOnlyList<Guid>?> GetEnrolledSwimmerIdsAsync(Guid eventId, CancellationToken ct = default)
    {
        var e = await _events.GetByIdAsync(eventId, ct);
        if (e is null) return null;
        return await _enrollments.ListSwimmerIdsAsync(eventId, ct);
    }

    public async Task<bool> SetEnrollmentsAsync(Guid eventId, IReadOnlyList<Guid> swimmerIds, CancellationToken ct = default)
    {
        var e = await _events.GetByIdAsync(eventId, ct);
        if (e is null) return false;
        await _enrollments.ReplaceAsync(eventId, swimmerIds, ct);
        return true;
    }

    public async Task<ScheduleDto?> GetScheduleAsync(Guid eventId, CancellationToken ct = default)
    {
        var e = await _events.GetByIdAsync(eventId, ct);
        if (e is null) return null;
        var rows = await _schedule.GetAsync(eventId, ct);
        return ToDto(rows);
    }

    public async Task<SetScheduleResult> SetScheduleAsync(Guid eventId, IReadOnlyList<SetScheduleDay> days, CancellationToken ct = default)
    {
        var e = await _events.GetByIdAsync(eventId, ct);
        if (e is null) return new SetScheduleResult(SetScheduleOutcome.NotFound, null, null);

        var enrolled = (await _enrollments.ListSwimmerIdsAsync(eventId, ct)).ToHashSet();
        var seenDates = new HashSet<DateOnly>();

        foreach (var d in days)
        {
            if (string.IsNullOrWhiteSpace(d.LabelEn))
                return new SetScheduleResult(SetScheduleOutcome.Invalid, null, "label_required");
            if (d.DayDate < e.StartDate || d.DayDate > e.EndDate)
                return new SetScheduleResult(SetScheduleOutcome.Invalid, null, "day_out_of_range");
            if (!seenDates.Add(d.DayDate))   // one competition day per calendar date
                return new SetScheduleResult(SetScheduleOutcome.Invalid, null, "duplicate_date");
            foreach (var r in d.Races)
            {
                if (r.StrokeId == Guid.Empty || r.DistanceId == Guid.Empty)
                    return new SetScheduleResult(SetScheduleOutcome.Invalid, null, "race_incomplete");
                foreach (var swimmerId in r.SwimmerIds)
                    if (!enrolled.Contains(swimmerId))
                        return new SetScheduleResult(SetScheduleOutcome.Invalid, null, "not_enrolled");
            }
        }

        var input = days.Select(d => new ScheduleDayInput(
            d.LabelEn.Trim(),
            string.IsNullOrWhiteSpace(d.LabelAr) ? null : d.LabelAr!.Trim(),
            d.DayDate,
            d.Races.Select(r => new ScheduleRaceInput(
                r.StrokeId, r.DistanceId, r.ScheduledTime, r.SwimmerIds.Distinct().ToList())).ToList())).ToList();

        await _schedule.ReplaceAsync(eventId, input, ct);
        var rows = await _schedule.GetAsync(eventId, ct);
        return new SetScheduleResult(SetScheduleOutcome.Ok, ToDto(rows), null);
    }

    public async Task<ResultsDto?> GetResultsAsync(Guid eventId, CancellationToken ct = default)
    {
        var e = await _events.GetByIdAsync(eventId, ct);
        if (e is null) return null;
        var rows = await _results.GetByEventAsync(eventId, ct);
        return ToDto(rows);
    }

    public async Task<SetRaceResultsResult> SetRaceResultsAsync(Guid eventId, Guid raceSessionId, IReadOnlyList<SetRaceResultsEntry> entries, Guid recordedBy, CancellationToken ct = default)
    {
        var e = await _events.GetByIdAsync(eventId, ct);
        if (e is null) return new SetRaceResultsResult(SetRaceResultsOutcome.NotFound, null, null);

        // Locate the race within THIS event's schedule (also gives us its distance/stroke + assigned swimmers).
        var days = await _schedule.GetAsync(eventId, ct);
        var race = days.SelectMany(d => d.Races).FirstOrDefault(r => r.Id == raceSessionId);
        if (race is null) return new SetRaceResultsResult(SetRaceResultsOutcome.NotFound, null, null);

        var assigned = race.SwimmerIds.ToHashSet();
        var seen = new HashSet<Guid>();
        foreach (var entry in entries)
        {
            if (entry.TimeMs <= 0)
                return new SetRaceResultsResult(SetRaceResultsOutcome.Invalid, null, "invalid_time");
            if (!assigned.Contains(entry.SwimmerId))
                return new SetRaceResultsResult(SetRaceResultsOutcome.Invalid, null, "not_assigned");
            if (!seen.Add(entry.SwimmerId))
                return new SetRaceResultsResult(SetRaceResultsOutcome.Invalid, null, "duplicate_swimmer");
        }

        var swimmerIds = entries.Select(x => x.SwimmerId).ToList();
        var bestTimes = await _results.GetBestTimesAsync(race.DistanceId, race.StrokeId, raceSessionId, swimmerIds, ct);

        var rows = entries.Select(entry =>
        {
            var isPb = !bestTimes.TryGetValue(entry.SwimmerId, out var best) || entry.TimeMs < best;
            return new RaceResultInput(entry.SwimmerId, entry.TimeMs, 0, isPb, recordedBy);
        }).ToList();

        await _results.ReplaceForSessionAsync(raceSessionId, rows, ct);
        var saved = await _results.GetByEventAsync(eventId, ct);
        return new SetRaceResultsResult(SetRaceResultsOutcome.Ok, ToDto(saved), null);
    }

    public async Task<IReadOnlyList<ChampionshipSwimmerHistoryDto>> GetSwimmerHistoryAsync(Guid swimmerId, CancellationToken ct = default)
    {
        var eventIds = await _enrollments.ListEventIdsBySwimmerAsync(swimmerId, ct);
        if (eventIds.Count == 0) return Array.Empty<ChampionshipSwimmerHistoryDto>();

        var events = await _events.ListByIdsAsync(eventIds, ct);
        var lines = await _results.GetSwimmerRaceLinesAsync(swimmerId, ct);
        var linesByEvent = lines.GroupBy(l => l.EventId).ToDictionary(g => g.Key, g => g.ToList());

        return events
            .OrderByDescending(e => e.StartDate).ThenByDescending(e => e.EndDate).ThenBy(e => e.NameEn)
            .Select(e =>
            {
                var races = (linesByEvent.TryGetValue(e.Id, out var ls) ? ls : new List<SwimmerRaceLineRow>())
                    .OrderBy(l => l.DayDate).ThenBy(l => l.ScheduledTime ?? TimeOnly.MinValue)
                    .Select(l => new ChampionshipSwimmerRaceDto(l.DayLabelEn, l.DayLabelAr, l.DistanceId, l.StrokeId, l.TimeMs, l.IsPersonalBest))
                    .ToList();
                return new ChampionshipSwimmerHistoryDto(e.Id, e.NameEn, e.NameAr, e.StartDate, e.EndDate, e.LocationEn, e.LocationAr, races);
            })
            .ToList();
    }

    private static ResultsDto ToDto(IReadOnlyList<RaceResultRow> rows) =>
        new(rows.Select(r => new RaceResultDto(r.RaceSessionId, r.SwimmerId, r.TimeMs, r.Points, r.IsPersonalBest)).ToList());

    private static ScheduleDto ToDto(IReadOnlyList<ScheduleDayRow> rows) =>
        new(rows.Select(d => new ScheduleDayDto(
            d.Id, d.LabelEn, d.LabelAr, d.DayDate,
            d.Races.Select(r => new ScheduleRaceDto(r.Id, r.StrokeId, r.DistanceId, r.ScheduledTime, r.SwimmerIds)).ToList()))
            .ToList());

    private static CompetitionEventDto ToDto(CompetitionEvent e) =>
        new(e.Id, e.NameEn, e.NameAr, e.StartDate, e.EndDate, e.LocationEn, e.LocationAr,
            e.StatusId, string.Empty, string.Empty, null);
}
