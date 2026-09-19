using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Kheprx.BaseBackend.Health.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Repositories;

public class InBodyReadingRepositoryTests
{
    private static HealthDbContext NewDb()
        => new(new DbContextOptionsBuilder<HealthDbContext>().UseInMemoryDatabase($"health-{Guid.NewGuid()}").Options);

    private static InBodyReading Reading(Guid swimmerId, DateOnly date)
        => new(swimmerId, date, 180m, 74m, 12.8m, 42.1m, 1.35m, 1.07m, Guid.NewGuid());

    [Fact]
    public async Task ListBySwimmerAsync_returns_only_that_swimmer_newest_first()
    {
        await using var db = NewDb();
        var repo = new InBodyReadingRepository(db);
        var sw = Guid.NewGuid();
        await repo.AddAsync(Reading(sw, new DateOnly(2024, 6, 15)));
        await repo.AddAsync(Reading(sw, new DateOnly(2024, 10, 4)));
        await repo.AddAsync(Reading(Guid.NewGuid(), new DateOnly(2024, 9, 9))); // other swimmer
        await repo.SaveChangesAsync();

        var list = await repo.ListBySwimmerAsync(sw);

        Assert.Equal(2, list.Count);
        Assert.Equal(new DateOnly(2024, 10, 4), list[0].ReadingDate); // newest first
    }

    [Fact]
    public async Task GetTrackedAsync_returns_reading_and_Remove_deletes_it()
    {
        await using var db = NewDb();
        var repo = new InBodyReadingRepository(db);
        var reading = Reading(Guid.NewGuid(), new DateOnly(2024, 10, 4));
        await repo.AddAsync(reading);
        await repo.SaveChangesAsync();

        var tracked = await repo.GetTrackedAsync(reading.Id);
        Assert.NotNull(tracked);
        repo.Remove(tracked!);
        await repo.SaveChangesAsync();

        Assert.Null(await repo.GetTrackedAsync(reading.Id));
    }
}
