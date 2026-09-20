using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Kheprx.BaseBackend.Health.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Repositories;

public class ObservationRepositoryTests
{
    private static HealthDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<HealthDbContext>()
            .UseInMemoryDatabase($"health-{Guid.NewGuid()}")
            .Options;
        return new HealthDbContext(options);
    }

    [Fact]
    public async Task Add_then_Save_persists_observation()
    {
        await using var db = NewDb();
        var repo = new ObservationRepository(db);
        var o = new Observation(Guid.NewGuid(), Guid.NewGuid(), "Penicillin", "Severe", Guid.NewGuid());

        await repo.AddAsync(o);
        await repo.SaveChangesAsync();

        var saved = await db.Observations.AsNoTracking().SingleAsync();
        Assert.Equal("Penicillin", saved.FieldLabel);
        Assert.NotEqual(default, saved.ObservedDate);
    }

    [Fact]
    public async Task ListBySwimmerAsync_returns_only_that_swimmer_newest_first()
    {
        await using var db = NewDb();
        var repo = new ObservationRepository(db);
        var sw = Guid.NewGuid();
        await repo.AddAsync(new Observation(sw, Guid.NewGuid(), "A", "1", Guid.NewGuid()));
        await repo.SaveChangesAsync();
        await repo.AddAsync(new Observation(sw, Guid.NewGuid(), "B", "2", Guid.NewGuid()));
        await repo.AddAsync(new Observation(Guid.NewGuid(), Guid.NewGuid(), "C", "3", Guid.NewGuid())); // other swimmer
        await repo.SaveChangesAsync();

        var list = await repo.ListBySwimmerAsync(sw);

        Assert.Equal(2, list.Count);
        Assert.All(list, o => Assert.Equal(sw, o.SwimmerId));
        Assert.True(list[0].ObservedDate >= list[1].ObservedDate); // newest first
    }

    [Fact]
    public async Task GetTrackedAsync_returns_observation_and_Remove_deletes_it()
    {
        await using var db = NewDb();
        var repo = new ObservationRepository(db);
        var o = new Observation(Guid.NewGuid(), Guid.NewGuid(), "Penicillin", "Severe", Guid.NewGuid());
        await repo.AddAsync(o);
        await repo.SaveChangesAsync();

        var tracked = await repo.GetTrackedAsync(o.Id);
        Assert.NotNull(tracked);
        repo.Remove(tracked!);
        await repo.SaveChangesAsync();

        Assert.Null(await repo.GetTrackedAsync(o.Id));
    }
}
