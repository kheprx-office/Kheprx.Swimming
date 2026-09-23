using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Kheprx.BaseBackend.Championships.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Repositories;

public class ChampionshipEnrollmentRepositoryTests
{
    private static ChampionshipsDbContext NewDb()
        => new(new DbContextOptionsBuilder<ChampionshipsDbContext>()
            .UseInMemoryDatabase($"enr-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task ListSwimmerIdsAsync_returns_only_ids_for_that_event()
    {
        await using var db = NewDb();
        var eventA = Guid.NewGuid();
        var eventB = Guid.NewGuid();
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        db.Enrollments.Add(new ChampionshipEnrollment(eventA, s1));
        db.Enrollments.Add(new ChampionshipEnrollment(eventA, s2));
        db.Enrollments.Add(new ChampionshipEnrollment(eventB, s1));
        await db.SaveChangesAsync();

        var ids = await new ChampionshipEnrollmentRepository(db).ListSwimmerIdsAsync(eventA);

        Assert.Equal(2, ids.Count);
        Assert.Contains(s1, ids);
        Assert.Contains(s2, ids);
    }

    [Fact]
    public async Task ReplaceAsync_deletes_prior_rows_and_inserts_the_new_deduped_set()
    {
        await using var db = NewDb();
        var eventId = Guid.NewGuid();
        var old = Guid.NewGuid();
        db.Enrollments.Add(new ChampionshipEnrollment(eventId, old));
        await db.SaveChangesAsync();

        var keep = Guid.NewGuid();
        await new ChampionshipEnrollmentRepository(db).ReplaceAsync(eventId, new[] { keep, keep });

        var ids = await new ChampionshipEnrollmentRepository(db).ListSwimmerIdsAsync(eventId);
        Assert.Single(ids);            // deduped
        Assert.Equal(keep, ids[0]);    // old row gone
    }

    [Fact]
    public async Task ReplaceAsync_with_empty_set_clears_the_event()
    {
        await using var db = NewDb();
        var eventId = Guid.NewGuid();
        db.Enrollments.Add(new ChampionshipEnrollment(eventId, Guid.NewGuid()));
        await db.SaveChangesAsync();

        await new ChampionshipEnrollmentRepository(db).ReplaceAsync(eventId, Array.Empty<Guid>());

        Assert.Empty(await new ChampionshipEnrollmentRepository(db).ListSwimmerIdsAsync(eventId));
    }
}
