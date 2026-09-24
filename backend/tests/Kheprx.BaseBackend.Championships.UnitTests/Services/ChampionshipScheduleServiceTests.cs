using Kheprx.BaseBackend.Championships.Application.DTOs;
using Kheprx.BaseBackend.Championships.Application.Services;
using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Services;

public class ChampionshipScheduleServiceTests
{
    private static CompetitionEvent NewEvent()
        => new("National Junior", null, new DateOnly(2023, 11, 15), new DateOnly(2023, 11, 16),
               "Cairo", null, Guid.NewGuid(), Guid.NewGuid());

    private static (ChampionshipService svc, Mock<ICompetitionEventRepository> events,
                    Mock<IChampionshipEnrollmentRepository> enr, Mock<ICompetitionScheduleRepository> sched) Build()
    {
        var events = new Mock<ICompetitionEventRepository>();
        var enr = new Mock<IChampionshipEnrollmentRepository>();
        var sched = new Mock<ICompetitionScheduleRepository>();
        var results = new Mock<IRaceResultRepository>();
        return (new ChampionshipService(events.Object, enr.Object, sched.Object, results.Object), events, enr, sched);
    }

    [Fact]
    public async Task GetScheduleAsync_returns_null_when_event_missing()
    {
        var (svc, events, _, _) = Build();
        events.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((CompetitionEvent?)null);

        Assert.Null(await svc.GetScheduleAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetScheduleAsync_maps_rows_to_dto()
    {
        var (svc, events, _, sched) = Build();
        var eventId = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        var dayId = Guid.NewGuid();
        var raceId = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        sched.Setup(r => r.GetAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new ScheduleDayRow(dayId, "Day 1", null, new DateOnly(2023, 11, 15), new[]
            {
                new ScheduleRaceRow(raceId, Guid.NewGuid(), Guid.NewGuid(), new TimeOnly(9, 0), new[] { swimmer }),
            }),
        });

        var dto = await svc.GetScheduleAsync(eventId);

        Assert.NotNull(dto);
        Assert.Single(dto!.Days);
        Assert.Equal("Day 1", dto.Days[0].LabelEn);
        Assert.Equal(swimmer, dto.Days[0].Races[0].SwimmerIds[0]);
    }

    [Fact]
    public async Task SetScheduleAsync_returns_NotFound_when_event_missing()
    {
        var (svc, events, _, _) = Build();
        events.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((CompetitionEvent?)null);

        var result = await svc.SetScheduleAsync(Guid.NewGuid(), Array.Empty<SetScheduleDay>());

        Assert.Equal(SetScheduleOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task SetScheduleAsync_rejects_a_non_enrolled_swimmer()
    {
        var (svc, events, enr, sched) = Build();
        var eventId = Guid.NewGuid();
        var enrolled = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        enr.Setup(r => r.ListSwimmerIdsAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { enrolled });

        var days = new[]
        {
            new SetScheduleDay("Day 1", null, new DateOnly(2023, 11, 15), new[]
            {
                new SetScheduleRace(Guid.NewGuid(), Guid.NewGuid(), null, new[] { stranger }),
            }),
        };

        var result = await svc.SetScheduleAsync(eventId, days);

        Assert.Equal(SetScheduleOutcome.Invalid, result.Outcome);
        sched.Verify(r => r.ReplaceAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ScheduleDayInput>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetScheduleAsync_rejects_a_blank_day_label()
    {
        var (svc, events, enr, sched) = Build();
        var eventId = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        enr.Setup(r => r.ListSwimmerIdsAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Guid>());

        var days = new[] { new SetScheduleDay("  ", null, new DateOnly(2023, 11, 15), Array.Empty<SetScheduleRace>()) };

        var result = await svc.SetScheduleAsync(eventId, days);

        Assert.Equal(SetScheduleOutcome.Invalid, result.Outcome);
        sched.Verify(r => r.ReplaceAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ScheduleDayInput>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(2023, 11, 14)]  // day before the event start (2023-11-15)
    [InlineData(2023, 11, 17)]  // day after the event end   (2023-11-16)
    public async Task SetScheduleAsync_rejects_a_day_outside_the_event_date_range(int year, int month, int day)
    {
        var (svc, events, enr, sched) = Build();
        var eventId = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent()); // 2023-11-15..16
        enr.Setup(r => r.ListSwimmerIdsAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Guid>());

        var days = new[] { new SetScheduleDay("Day 1", null, new DateOnly(year, month, day), Array.Empty<SetScheduleRace>()) };

        var result = await svc.SetScheduleAsync(eventId, days);

        Assert.Equal(SetScheduleOutcome.Invalid, result.Outcome);
        sched.Verify(r => r.ReplaceAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ScheduleDayInput>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(2023, 11, 15)]  // event start (inclusive)
    [InlineData(2023, 11, 16)]  // event end   (inclusive)
    public async Task SetScheduleAsync_accepts_a_day_on_the_range_boundary(int year, int month, int day)
    {
        var (svc, events, enr, sched) = Build();
        var eventId = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        enr.Setup(r => r.ListSwimmerIdsAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Guid>());
        sched.Setup(r => r.GetAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ScheduleDayRow>());

        var days = new[] { new SetScheduleDay("Day 1", null, new DateOnly(year, month, day), Array.Empty<SetScheduleRace>()) };

        var result = await svc.SetScheduleAsync(eventId, days);

        Assert.Equal(SetScheduleOutcome.Ok, result.Outcome);
        sched.Verify(r => r.ReplaceAsync(eventId, It.IsAny<IReadOnlyList<ScheduleDayInput>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetScheduleAsync_rejects_two_days_on_the_same_date()
    {
        var (svc, events, enr, sched) = Build();
        var eventId = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent()); // 2023-11-15..16
        enr.Setup(r => r.ListSwimmerIdsAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Guid>());

        var days = new[]
        {
            new SetScheduleDay("Day 1", null, new DateOnly(2023, 11, 15), Array.Empty<SetScheduleRace>()),
            new SetScheduleDay("Day 2", null, new DateOnly(2023, 11, 15), Array.Empty<SetScheduleRace>()), // duplicate date
        };

        var result = await svc.SetScheduleAsync(eventId, days);

        Assert.Equal(SetScheduleOutcome.Invalid, result.Outcome);
        sched.Verify(r => r.ReplaceAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ScheduleDayInput>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetScheduleAsync_accepts_two_days_on_distinct_dates()
    {
        var (svc, events, enr, sched) = Build();
        var eventId = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        enr.Setup(r => r.ListSwimmerIdsAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Guid>());
        sched.Setup(r => r.GetAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ScheduleDayRow>());

        var days = new[]
        {
            new SetScheduleDay("Day 1", null, new DateOnly(2023, 11, 15), Array.Empty<SetScheduleRace>()),
            new SetScheduleDay("Day 2", null, new DateOnly(2023, 11, 16), Array.Empty<SetScheduleRace>()),
        };

        var result = await svc.SetScheduleAsync(eventId, days);

        Assert.Equal(SetScheduleOutcome.Ok, result.Outcome);
        sched.Verify(r => r.ReplaceAsync(eventId, It.IsAny<IReadOnlyList<ScheduleDayInput>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetScheduleAsync_replaces_and_returns_the_saved_tree()
    {
        var (svc, events, enr, sched) = Build();
        var eventId = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        events.Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(NewEvent());
        enr.Setup(r => r.ListSwimmerIdsAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { swimmer });
        var savedDayId = Guid.NewGuid();
        sched.Setup(r => r.GetAsync(eventId, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new ScheduleDayRow(savedDayId, "Day 1", null, new DateOnly(2023, 11, 15), Array.Empty<ScheduleRaceRow>()),
        });

        var days = new[]
        {
            new SetScheduleDay("Day 1", null, new DateOnly(2023, 11, 15), new[]
            {
                new SetScheduleRace(Guid.NewGuid(), Guid.NewGuid(), new TimeOnly(9, 0), new[] { swimmer }),
            }),
        };

        var result = await svc.SetScheduleAsync(eventId, days);

        Assert.Equal(SetScheduleOutcome.Ok, result.Outcome);
        Assert.Equal(savedDayId, result.Saved!.Days[0].Id);
        sched.Verify(r => r.ReplaceAsync(eventId, It.IsAny<IReadOnlyList<ScheduleDayInput>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
