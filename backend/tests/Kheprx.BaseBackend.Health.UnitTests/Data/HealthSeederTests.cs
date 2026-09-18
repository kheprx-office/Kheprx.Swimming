using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Data;

public class HealthSeederTests
{
    private static HealthDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<HealthDbContext>()
            .UseInMemoryDatabase($"health-seed-{Guid.NewGuid()}")
            .Options;
        return new HealthDbContext(options);
    }

    [Fact]
    public async Task Seeds_three_demo_tests_and_is_idempotent()
    {
        await using var db = NewDb();

        await HealthSeeder.SeedAsync(db);
        await HealthSeeder.SeedAsync(db); // second run must not duplicate

        Assert.Equal(3, await db.MedicalTests.CountAsync());
    }
}
