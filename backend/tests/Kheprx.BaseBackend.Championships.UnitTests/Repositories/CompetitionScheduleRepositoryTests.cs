using Kheprx.BaseBackend.Championships.Domain.Repositories;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Kheprx.BaseBackend.Championships.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Repositories;

public class CompetitionScheduleRepositoryTests
{
    private static ChampionshipsDbContext NewDb()
        => new(new DbContextOptionsBuilder<ChampionshipsDbContext>()
            .UseInMemoryDatabase($"sched-{Guid.NewGuid()}").Options);

    private static ScheduleDayInput Day(string label, DateOnly date, params ScheduleRaceInput[] races)
        => new(label, null, date, races);

    private static ScheduleRaceInput Race(Guid stroke, Guid distance, TimeOnly? time, params Guid[] swimmers)
        => new(stroke, distance, time, swimmers);

    [Fact]
    public async Task ReplaceAsync_then_GetAsync_round_trips_the_tree()
    {
        await using var db = NewDb();
        var eventId = Guid.NewGuid();
        var stroke = Guid.NewGuid();
        var distance = Guid.NewGuid();
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();

        await new CompetitionScheduleRepository(db).ReplaceAsync(eventId, new[]
        {
            Day("Day 1", new DateOnly(2023, 11, 15),
                Race(stroke, distance, new TimeOnly(9, 0), s1, s2)),
        });

        var rows = await new CompetitionScheduleRepository(db).GetAsync(eventId);

        Assert.Single(rows);
        Assert.Equal("Day 1", rows[0].LabelEn);
        Assert.Single(rows[0].Races);
        Assert.Equal(new TimeOnly(9, 0), rows[0].Races[0].ScheduledTime);
        Assert.Equal(2, rows[0].Races[0].SwimmerIds.Count);
        Assert.Contains(s1, rows[0].Races[0].SwimmerIds);
    }

    [Fact]
    public async Task ReplaceAsync_deletes_the_prior_tree_for_the_event()
    {
        await using var db = NewDb();
        var eventId = Guid.NewGuid();
        var repo = new CompetitionScheduleRepository(db);
        await repo.ReplaceAsync(eventId, new[]
        {
            Day("Old", new DateOnly(2023, 1, 1), Race(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid())),
        });

        await repo.ReplaceAsync(eventId, new[] { Day("New", new DateOnly(2023, 2, 2)) });

        var rows = await repo.GetAsync(eventId);
        Assert.Single(rows);
        Assert.Equal("New", rows[0].LabelEn);
        Assert.Empty(rows[0].Races);
        Assert.Empty(await db.RaceSessions.ToListAsync());     // old sessions gone
        Assert.Empty(await db.RaceAssignments.ToListAsync());  // old assignments gone
    }

    [Fact]
    public async Task GetAsync_returns_only_the_target_event()
    {
        await using var db = NewDb();
        var repo = new CompetitionScheduleRepository(db);
        var eventA = Guid.NewGuid();
        var eventB = Guid.NewGuid();
        await repo.ReplaceAsync(eventA, new[] { Day("A", new DateOnly(2023, 1, 1)) });
        await repo.ReplaceAsync(eventB, new[] { Day("B", new DateOnly(2023, 1, 1)) });

        var rows = await repo.GetAsync(eventA);
        Assert.Single(rows);
        Assert.Equal("A", rows[0].LabelEn);
    }

    [Fact]
    public async Task ReplaceAsync_with_empty_list_clears_the_event()
    {
        await using var db = NewDb();
        var eventId = Guid.NewGuid();
        var repo = new CompetitionScheduleRepository(db);
        await repo.ReplaceAsync(eventId, new[] { Day("D", new DateOnly(2023, 1, 1), Race(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid())) });

        await repo.ReplaceAsync(eventId, Array.Empty<ScheduleDayInput>());

        Assert.Empty(await repo.GetAsync(eventId));
    }
}
