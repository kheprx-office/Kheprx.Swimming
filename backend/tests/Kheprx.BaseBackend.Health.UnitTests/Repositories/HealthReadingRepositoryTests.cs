using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Kheprx.BaseBackend.Health.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Repositories;

public class HealthReadingRepositoryTests
{
    private static HealthDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<HealthDbContext>()
            .UseInMemoryDatabase($"health-{Guid.NewGuid()}")
            .Options;
        return new HealthDbContext(options);
    }

    [Fact]
    public async Task Add_then_Save_persists_reading()
    {
        await using var db = NewDb();
        var repo = new HealthReadingRepository(db);
        var recordedBy = Guid.NewGuid();
        var reading = new HealthReading(Guid.NewGuid(), Guid.NewGuid(), 95m, recordedBy);

        await repo.AddAsync(reading);
        await repo.SaveChangesAsync();

        var saved = await db.HealthReadings.AsNoTracking().SingleAsync();
        Assert.Equal(95m, saved.Value);
        Assert.Equal(recordedBy, saved.RecordedBy);
        Assert.NotEqual(default, saved.ReadingDate);
    }

    [Fact]
    public async Task ListBySwimmer_returns_only_that_swimmer_newest_first()
    {
        await using var db = NewDb();
        var repo = new HealthReadingRepository(db);
        var swimmer = Guid.NewGuid();
        var other = Guid.NewGuid();
        var test = Guid.NewGuid();
        var older = new HealthReading(swimmer, test, 10m, Guid.NewGuid());
        var newer = new HealthReading(swimmer, test, 20m, Guid.NewGuid());
        typeof(HealthReading).GetProperty("ReadingDate")!.SetValue(older, DateTime.UtcNow.AddDays(-2));
        typeof(HealthReading).GetProperty("ReadingDate")!.SetValue(newer, DateTime.UtcNow);
        await repo.AddAsync(older);
        await repo.AddAsync(newer);
        await repo.AddAsync(new HealthReading(other, test, 30m, Guid.NewGuid()));
        await repo.SaveChangesAsync();

        var list = await repo.ListBySwimmerAsync(swimmer);

        Assert.Equal(2, list.Count);
        Assert.Equal(20m, list[0].Value); // newest first
        Assert.Equal(10m, list[1].Value);
    }

    [Fact]
    public async Task GetTracked_then_Remove_deletes_the_reading()
    {
        await using var db = NewDb();
        var repo = new HealthReadingRepository(db);
        var reading = new HealthReading(Guid.NewGuid(), Guid.NewGuid(), 50m, Guid.NewGuid());
        await repo.AddAsync(reading);
        await repo.SaveChangesAsync();

        var tracked = await repo.GetTrackedAsync(reading.Id);
        Assert.NotNull(tracked);
        repo.Remove(tracked!);
        await repo.SaveChangesAsync();

        Assert.Empty(await db.HealthReadings.AsNoTracking().ToListAsync());
    }
}
