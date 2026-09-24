using Kheprx.BaseBackend.Championships.Application.Services;
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Services;

public class SwimmerHistoryServiceTests
{
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

    private static CompetitionEvent Ev(string name, DateOnly start, DateOnly end)
        => new(name, null, start, end, "Cairo", null, Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task Returns_empty_when_swimmer_has_no_enrollments()
    {
        var (svc, _, enr, _, _) = Build();
        enr.Setup(r => r.ListEventIdsBySwimmerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Array.Empty<Guid>());

        var history = await svc.GetSwimmerHistoryAsync(Guid.NewGuid());

        Assert.Empty(history);
    }

    [Fact]
    public async Task Enrolled_event_with_no_results_appears_with_empty_races()
    {
        var (svc, events, enr, _, results) = Build();
        var swimmer = Guid.NewGuid();
        var ev = Ev("Spring Invitational", new DateOnly(2023, 4, 8), new DateOnly(2023, 4, 9));
        enr.Setup(r => r.ListEventIdsBySwimmerAsync(swimmer, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { ev.Id });
        events.Setup(r => r.ListByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { ev });
        results.Setup(r => r.GetSwimmerRaceLinesAsync(swimmer, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<SwimmerRaceLineRow>());

        var history = await svc.GetSwimmerHistoryAsync(swimmer);

        Assert.Single(history);
        Assert.Equal(ev.Id, history[0].EventId);
        Assert.Empty(history[0].Races);
    }

    [Fact]
    public async Task Orders_events_newest_first_and_races_by_day_then_time()
    {
        var (svc, events, enr, _, results) = Build();
        var swimmer = Guid.NewGuid();
        var older = Ev("Regional", new DateOnly(2023, 10, 20), new DateOnly(2023, 10, 20));
        var newer = Ev("National", new DateOnly(2023, 11, 15), new DateOnly(2023, 11, 16));
        enr.Setup(r => r.ListEventIdsBySwimmerAsync(swimmer, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { older.Id, newer.Id });
        events.Setup(r => r.ListByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { older, newer });
        var dist = Guid.NewGuid(); var stroke = Guid.NewGuid();
        results.Setup(r => r.GetSwimmerRaceLinesAsync(swimmer, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            // newer event, day 2 then day 1 out of order; day 1 has two races out of time order
            new SwimmerRaceLineRow(newer.Id, "Day 2", null, new DateOnly(2023, 11, 16), new TimeOnly(10, 0), dist, stroke, 61770, false),
            new SwimmerRaceLineRow(newer.Id, "Day 1", null, new DateOnly(2023, 11, 15), new TimeOnly(9, 30), dist, stroke, 118120, false),
            new SwimmerRaceLineRow(newer.Id, "Day 1", null, new DateOnly(2023, 11, 15), new TimeOnly(9, 0), dist, stroke, 52340, true),
        });

        var history = await svc.GetSwimmerHistoryAsync(swimmer);

        Assert.Equal(new[] { "National", "Regional" }, history.Select(h => h.NameEn));   // newest first
        Assert.Equal(new[] { 52340, 118120, 61770 }, history[0].Races.Select(r => r.TimeMs)); // day1@9:00, day1@9:30, day2@10:00
        Assert.True(history[0].Races[0].IsPersonalBest);
    }
}
