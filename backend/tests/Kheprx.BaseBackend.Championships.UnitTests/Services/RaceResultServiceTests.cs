using Kheprx.BaseBackend.Championships.Application.DTOs;
using Kheprx.BaseBackend.Championships.Application.Services;
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Services;

public class RaceResultServiceTests
{
    private static CompetitionEvent NewEvent()
        => new("National Junior", null, new DateOnly(2023, 11, 15), new DateOnly(2023, 11, 16),
               "Cairo", null, Guid.NewGuid(), Guid.NewGuid());

    private static (ChampionshipService svc, Mock<ICompetitionEventRepository> events,
                    Mock<IChampionshipEnrollmentRepository> enr, Mock<ICompetitionScheduleRepository> sched,
                    Mock<IRaceResultRepository> results) Build()
    {
        var events = new Mock<ICompetitionEventRepository>();
        var enr = new Mock<IChampionshipEnrollmentRepository>();
        var sched = new Mock<ICompetitionScheduleRepository>();
        var results = new Mock<IRaceResultRepository>();
        return (new ChampionshipService(events.Object, enr.Object, sched.Object, results.Object), events, enr, sched, results);
    }

    // One event with one day holding one race (raceId) that has the given assigned swimmers.
    private static void SetupSchedule(Mock<ICompetitionScheduleRepository> sched, Guid eventId, Guid raceId,
        Guid strokeId, Guid distanceId, params Guid[] assigned)
        => sched.Setup(r => r.GetAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new ScheduleDayRow(Guid.NewGuid(), "Day 1", null, new DateOnly(2023, 11, 15), new[]
            {
                new ScheduleRaceRow(raceId, strokeId, distanceId, new TimeOnly(9, 0), assigned),
            }),
        });

    [Fact]
    public async Task GetResultsAsync_returns_null_when_event_missing()
    {
        var (svc, events, _, _, _) = Build();
        events.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((CompetitionEvent?)null);
        Assert.Null(await svc.GetResultsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetResultsAsync_maps_rows_to_dto()
    {
        var (svc, events, _, _, results) = Build();
        var eventId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        results.Setup(r => r.GetByEventAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new RaceResultRow(Guid.NewGuid(), sessionId, swimmer, 24560, 0, true, Guid.NewGuid()),
        });

        var dto = await svc.GetResultsAsync(eventId);

        Assert.NotNull(dto);
        Assert.Single(dto!.Results);
        Assert.Equal(24560, dto.Results[0].TimeMs);
        Assert.True(dto.Results[0].IsPersonalBest);
    }

    [Fact]
    public async Task SetRaceResultsAsync_returns_NotFound_when_event_missing()
    {
        var (svc, events, _, _, _) = Build();
        events.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((CompetitionEvent?)null);
        var result = await svc.SetRaceResultsAsync(Guid.NewGuid(), Guid.NewGuid(), Array.Empty<SetRaceResultsEntry>(), Guid.NewGuid());
        Assert.Equal(SetRaceResultsOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task SetRaceResultsAsync_returns_NotFound_when_session_not_in_event()
    {
        var (svc, events, _, sched, _) = Build();
        var eventId = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        SetupSchedule(sched, eventId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()); // some other race
        var result = await svc.SetRaceResultsAsync(eventId, Guid.NewGuid(), Array.Empty<SetRaceResultsEntry>(), Guid.NewGuid());
        Assert.Equal(SetRaceResultsOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task SetRaceResultsAsync_rejects_a_non_assigned_swimmer()
    {
        var (svc, events, _, sched, results) = Build();
        var eventId = Guid.NewGuid();
        var raceId = Guid.NewGuid();
        var assigned = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        SetupSchedule(sched, eventId, raceId, Guid.NewGuid(), Guid.NewGuid(), assigned);

        var result = await svc.SetRaceResultsAsync(eventId, raceId, new[] { new SetRaceResultsEntry(stranger, 25000) }, Guid.NewGuid());

        Assert.Equal(SetRaceResultsOutcome.Invalid, result.Outcome);
        results.Verify(r => r.ReplaceForSessionAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<RaceResultInput>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetRaceResultsAsync_rejects_a_non_positive_time()
    {
        var (svc, events, _, sched, _) = Build();
        var eventId = Guid.NewGuid();
        var raceId = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        SetupSchedule(sched, eventId, raceId, Guid.NewGuid(), Guid.NewGuid(), swimmer);

        var result = await svc.SetRaceResultsAsync(eventId, raceId, new[] { new SetRaceResultsEntry(swimmer, 0) }, Guid.NewGuid());

        Assert.Equal(SetRaceResultsOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public async Task SetRaceResultsAsync_rejects_a_duplicate_swimmer_in_the_batch()
    {
        var (svc, events, _, sched, _) = Build();
        var eventId = Guid.NewGuid();
        var raceId = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        SetupSchedule(sched, eventId, raceId, Guid.NewGuid(), Guid.NewGuid(), swimmer);

        var result = await svc.SetRaceResultsAsync(eventId, raceId,
            new[] { new SetRaceResultsEntry(swimmer, 25000), new SetRaceResultsEntry(swimmer, 26000) }, Guid.NewGuid());

        Assert.Equal(SetRaceResultsOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public async Task SetRaceResultsAsync_marks_personal_best_only_when_it_beats_the_prior_best()
    {
        var (svc, events, _, sched, results) = Build();
        var eventId = Guid.NewGuid();
        var raceId = Guid.NewGuid();
        var stroke = Guid.NewGuid();
        var distance = Guid.NewGuid();
        var faster = Guid.NewGuid();   // has a slower prior time -> new time is a PB
        var slower = Guid.NewGuid();   // has a faster prior time -> new time is NOT a PB
        var fresh = Guid.NewGuid();    // no prior time -> PB
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        SetupSchedule(sched, eventId, raceId, stroke, distance, faster, slower, fresh);
        results.Setup(r => r.GetByEventAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<RaceResultRow>());
        results.Setup(r => r.GetBestTimesAsync(distance, stroke, raceId, It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Dictionary<Guid, int> { [faster] = 30000, [slower] = 20000 });

        IReadOnlyList<RaceResultInput>? captured = null;
        results.Setup(r => r.ReplaceForSessionAsync(raceId, It.IsAny<IReadOnlyList<RaceResultInput>>(), It.IsAny<CancellationToken>()))
               .Callback<Guid, IReadOnlyList<RaceResultInput>, CancellationToken>((_, rows, _) => captured = rows)
               .Returns(Task.CompletedTask);

        var result = await svc.SetRaceResultsAsync(eventId, raceId, new[]
        {
            new SetRaceResultsEntry(faster, 25000),
            new SetRaceResultsEntry(slower, 25000),
            new SetRaceResultsEntry(fresh, 25000),
        }, Guid.NewGuid());

        Assert.Equal(SetRaceResultsOutcome.Ok, result.Outcome);
        Assert.NotNull(captured);
        Assert.True(captured!.Single(r => r.SwimmerId == faster).IsPersonalBest);
        Assert.False(captured.Single(r => r.SwimmerId == slower).IsPersonalBest);
        Assert.True(captured.Single(r => r.SwimmerId == fresh).IsPersonalBest);
        Assert.All(captured, r => Assert.Equal(0, r.Points));   // points always 0 this pass
    }

    [Fact]
    public async Task SetRaceResultsAsync_ties_are_not_personal_bests()
    {
        var (svc, events, _, sched, results) = Build();
        var eventId = Guid.NewGuid();
        var raceId = Guid.NewGuid();
        var stroke = Guid.NewGuid();
        var distance = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        SetupSchedule(sched, eventId, raceId, stroke, distance, swimmer);
        results.Setup(r => r.GetByEventAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<RaceResultRow>());
        results.Setup(r => r.GetBestTimesAsync(distance, stroke, raceId, It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Dictionary<Guid, int> { [swimmer] = 25000 });
        IReadOnlyList<RaceResultInput>? captured = null;
        results.Setup(r => r.ReplaceForSessionAsync(raceId, It.IsAny<IReadOnlyList<RaceResultInput>>(), It.IsAny<CancellationToken>()))
               .Callback<Guid, IReadOnlyList<RaceResultInput>, CancellationToken>((_, rows, _) => captured = rows)
               .Returns(Task.CompletedTask);

        await svc.SetRaceResultsAsync(eventId, raceId, new[] { new SetRaceResultsEntry(swimmer, 25000) }, Guid.NewGuid());

        Assert.False(captured!.Single().IsPersonalBest); // equal time is not a PB
    }

    [Fact]
    public async Task SetRaceResultsAsync_replaces_and_returns_the_events_results()
    {
        var (svc, events, _, sched, results) = Build();
        var eventId = Guid.NewGuid();
        var raceId = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        var recorder = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        SetupSchedule(sched, eventId, raceId, Guid.NewGuid(), Guid.NewGuid(), swimmer);
        results.Setup(r => r.GetBestTimesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), raceId, It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Dictionary<Guid, int>());
        results.Setup(r => r.GetByEventAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new RaceResultRow(Guid.NewGuid(), raceId, swimmer, 25000, 0, true, recorder),
        });

        var result = await svc.SetRaceResultsAsync(eventId, raceId, new[] { new SetRaceResultsEntry(swimmer, 25000) }, recorder);

        Assert.Equal(SetRaceResultsOutcome.Ok, result.Outcome);
        Assert.Single(result.Saved!.Results);
        results.Verify(r => r.ReplaceForSessionAsync(raceId, It.IsAny<IReadOnlyList<RaceResultInput>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
