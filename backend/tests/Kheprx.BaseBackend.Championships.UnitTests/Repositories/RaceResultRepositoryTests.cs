using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Kheprx.BaseBackend.Championships.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Repositories;

public class RaceResultRepositoryTests
{
    private static ChampionshipsDbContext NewDb()
        => new(new DbContextOptionsBuilder<ChampionshipsDbContext>()
            .UseInMemoryDatabase($"results-{Guid.NewGuid()}").Options);

    // Seeds a day+session under an event and returns the session id.
    private static async Task<Guid> SeedSessionAsync(ChampionshipsDbContext db, Guid eventId, Guid strokeId, Guid distanceId)
    {
        var day = new CompetitionDay(eventId, "Day 1", null, new DateOnly(2023, 11, 15));
        db.CompetitionDays.Add(day);
        var session = new RaceSession(day.Id, strokeId, distanceId, new TimeOnly(9, 0));
        db.RaceSessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    [Fact]
    public async Task ReplaceForSessionAsync_then_GetByEventAsync_round_trips()
    {
        await using var db = NewDb();
        var eventId = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        var sessionId = await SeedSessionAsync(db, eventId, Guid.NewGuid(), Guid.NewGuid());
        var repo = new RaceResultRepository(db);

        await repo.ReplaceForSessionAsync(sessionId, new[] { new RaceResultInput(swimmer, 24560, 0, true, Guid.NewGuid()) });

        var rows = await repo.GetByEventAsync(eventId);
        Assert.Single(rows);
        Assert.Equal(sessionId, rows[0].RaceSessionId);
        Assert.Equal(24560, rows[0].TimeMs);
        Assert.True(rows[0].IsPersonalBest);
    }

    [Fact]
    public async Task ReplaceForSessionAsync_replaces_the_sessions_prior_rows()
    {
        await using var db = NewDb();
        var eventId = Guid.NewGuid();
        var sessionId = await SeedSessionAsync(db, eventId, Guid.NewGuid(), Guid.NewGuid());
        var repo = new RaceResultRepository(db);
        await repo.ReplaceForSessionAsync(sessionId, new[] { new RaceResultInput(Guid.NewGuid(), 100, 0, false, Guid.NewGuid()) });

        var keep = Guid.NewGuid();
        await repo.ReplaceForSessionAsync(sessionId, new[] { new RaceResultInput(keep, 200, 0, true, Guid.NewGuid()) });

        var rows = await repo.GetByEventAsync(eventId);
        Assert.Single(rows);
        Assert.Equal(keep, rows[0].SwimmerId);
        Assert.Equal(200, rows[0].TimeMs);
    }

    [Fact]
    public async Task ReplaceForSessionAsync_with_empty_list_clears_the_session()
    {
        await using var db = NewDb();
        var eventId = Guid.NewGuid();
        var sessionId = await SeedSessionAsync(db, eventId, Guid.NewGuid(), Guid.NewGuid());
        var repo = new RaceResultRepository(db);
        await repo.ReplaceForSessionAsync(sessionId, new[] { new RaceResultInput(Guid.NewGuid(), 100, 0, false, Guid.NewGuid()) });

        await repo.ReplaceForSessionAsync(sessionId, Array.Empty<RaceResultInput>());

        Assert.Empty(await repo.GetByEventAsync(eventId));
    }

    [Fact]
    public async Task GetByEventAsync_returns_only_the_target_event()
    {
        await using var db = NewDb();
        var eventA = Guid.NewGuid();
        var eventB = Guid.NewGuid();
        var sessionA = await SeedSessionAsync(db, eventA, Guid.NewGuid(), Guid.NewGuid());
        var sessionB = await SeedSessionAsync(db, eventB, Guid.NewGuid(), Guid.NewGuid());
        var repo = new RaceResultRepository(db);
        await repo.ReplaceForSessionAsync(sessionA, new[] { new RaceResultInput(Guid.NewGuid(), 100, 0, false, Guid.NewGuid()) });
        await repo.ReplaceForSessionAsync(sessionB, new[] { new RaceResultInput(Guid.NewGuid(), 200, 0, false, Guid.NewGuid()) });

        var rows = await repo.GetByEventAsync(eventA);
        Assert.Single(rows);
        Assert.Equal(sessionA, rows[0].RaceSessionId);
    }

    [Fact]
    public async Task GetBestTimesAsync_returns_min_prior_time_for_same_distance_stroke_across_events_excluding_current_session()
    {
        await using var db = NewDb();
        var stroke = Guid.NewGuid();
        var distance = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        // Two sessions in DIFFERENT events, same distance+stroke -> PB is career-wide.
        var priorSession = await SeedSessionAsync(db, Guid.NewGuid(), stroke, distance);
        var currentSession = await SeedSessionAsync(db, Guid.NewGuid(), stroke, distance);
        var repo = new RaceResultRepository(db);
        await repo.ReplaceForSessionAsync(priorSession, new[] { new RaceResultInput(swimmer, 26000, 0, true, Guid.NewGuid()) });
        await repo.ReplaceForSessionAsync(currentSession, new[] { new RaceResultInput(swimmer, 25000, 0, true, Guid.NewGuid()) });

        var best = await repo.GetBestTimesAsync(distance, stroke, currentSession, new[] { swimmer });

        Assert.Equal(26000, best[swimmer]); // the prior session's time, NOT the current one
    }

    [Fact]
    public async Task GetBestTimesAsync_omits_swimmers_with_no_prior_time()
    {
        await using var db = NewDb();
        var repo = new RaceResultRepository(db);
        var best = await repo.GetBestTimesAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new[] { Guid.NewGuid() });
        Assert.Empty(best);
    }
}
