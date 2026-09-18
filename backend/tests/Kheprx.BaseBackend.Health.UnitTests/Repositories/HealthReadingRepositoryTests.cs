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
}
