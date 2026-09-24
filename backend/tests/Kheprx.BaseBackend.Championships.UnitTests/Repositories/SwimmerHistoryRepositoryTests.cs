using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Kheprx.BaseBackend.Championships.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Repositories;

public class SwimmerHistoryRepositoryTests
{
    private static ChampionshipsDbContext NewDb()
        => new(new DbContextOptionsBuilder<ChampionshipsDbContext>()
            .UseInMemoryDatabase($"history-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task ListEventIdsBySwimmerAsync_returns_distinct_events_for_the_swimmer()
    {
        await using var db = NewDb();
        var swimmer = Guid.NewGuid();
        var other = Guid.NewGuid();
        var eventA = Guid.NewGuid();
        var eventB = Guid.NewGuid();
        db.Enrollments.Add(new ChampionshipEnrollment(eventA, swimmer));
        db.Enrollments.Add(new ChampionshipEnrollment(eventB, swimmer));
        db.Enrollments.Add(new ChampionshipEnrollment(eventA, other));
        await db.SaveChangesAsync();
        var repo = new ChampionshipEnrollmentRepository(db);

        var ids = await repo.ListEventIdsBySwimmerAsync(swimmer);

        Assert.Equal(2, ids.Count);
        Assert.Contains(eventA, ids);
        Assert.Contains(eventB, ids);
    }

    [Fact]
    public async Task ListByIdsAsync_returns_only_the_requested_events()
    {
        await using var db = NewDb();
        var e1 = new CompetitionEvent("A", null, new DateOnly(2023, 1, 1), new DateOnly(2023, 1, 2), "X", null, Guid.NewGuid(), Guid.NewGuid());
        var e2 = new CompetitionEvent("B", null, new DateOnly(2023, 2, 1), new DateOnly(2023, 2, 2), "Y", null, Guid.NewGuid(), Guid.NewGuid());
        db.CompetitionEvents.AddRange(e1, e2);
        await db.SaveChangesAsync();
        var repo = new CompetitionEventRepository(db);

        var got = await repo.ListByIdsAsync(new[] { e1.Id });

        Assert.Single(got);
        Assert.Equal(e1.Id, got[0].Id);
    }

    [Fact]
    public async Task ListByIdsAsync_returns_empty_for_empty_input()
    {
        await using var db = NewDb();
        var repo = new CompetitionEventRepository(db);
        Assert.Empty(await repo.ListByIdsAsync(Array.Empty<Guid>()));
    }

    [Fact]
    public async Task GetSwimmerRaceLinesAsync_joins_result_to_session_and_day_for_the_swimmer()
    {
        await using var db = NewDb();
        var swimmer = Guid.NewGuid();
        var other = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var dist = Guid.NewGuid();
        var stroke = Guid.NewGuid();
        var day = new CompetitionDay(eventId, "Day 1", "اليوم 1", new DateOnly(2023, 11, 15));
        db.CompetitionDays.Add(day);
        var session = new RaceSession(day.Id, stroke, dist, new TimeOnly(9, 0));
        db.RaceSessions.Add(session);
        db.RaceResults.Add(new RaceResult(session.Id, swimmer, 52340, 0, true, Guid.NewGuid()));
        db.RaceResults.Add(new RaceResult(session.Id, other, 60000, 0, false, Guid.NewGuid()));
        await db.SaveChangesAsync();
        var repo = new RaceResultRepository(db);

        var lines = await repo.GetSwimmerRaceLinesAsync(swimmer);

        Assert.Single(lines);
        var l = lines[0];
        Assert.Equal(eventId, l.EventId);
        Assert.Equal("Day 1", l.DayLabelEn);
        Assert.Equal("اليوم 1", l.DayLabelAr);
        Assert.Equal(new DateOnly(2023, 11, 15), l.DayDate);
        Assert.Equal(new TimeOnly(9, 0), l.ScheduledTime);
        Assert.Equal(dist, l.DistanceId);
        Assert.Equal(stroke, l.StrokeId);
        Assert.Equal(52340, l.TimeMs);
        Assert.True(l.IsPersonalBest);
    }
}
