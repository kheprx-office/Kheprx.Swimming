using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Kheprx.BaseBackend.Health.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Repositories;

public class FeedbackEntryRepositoryTests
{
    private static HealthDbContext NewDb()
        => new(new DbContextOptionsBuilder<HealthDbContext>().UseInMemoryDatabase($"health-{Guid.NewGuid()}").Options);

    private static FeedbackEntry Entry(Guid swimmerId)
        => new(swimmerId, 4, Guid.NewGuid(), "note", Guid.NewGuid());

    [Fact]
    public async Task ListBySwimmerAsync_returns_only_that_swimmer()
    {
        await using var db = NewDb();
        var repo = new FeedbackEntryRepository(db);
        var sw = Guid.NewGuid();
        await repo.AddAsync(Entry(sw));
        await repo.AddAsync(Entry(sw));
        await repo.AddAsync(Entry(Guid.NewGuid())); // other swimmer
        await repo.SaveChangesAsync();

        var list = await repo.ListBySwimmerAsync(sw);

        Assert.Equal(2, list.Count);
        Assert.All(list, e => Assert.Equal(sw, e.SwimmerId));
    }

    [Fact]
    public async Task GetTrackedAsync_then_Remove_deletes()
    {
        await using var db = NewDb();
        var repo = new FeedbackEntryRepository(db);
        var e = Entry(Guid.NewGuid());
        await repo.AddAsync(e);
        await repo.SaveChangesAsync();

        var tracked = await repo.GetTrackedAsync(e.Id);
        Assert.NotNull(tracked);
        repo.Remove(tracked!);
        await repo.SaveChangesAsync();

        Assert.Null(await repo.GetTrackedAsync(e.Id));
    }
}
