using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Kheprx.BaseBackend.Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Repositories;

public class DistanceRepositoryTests
{
    private static IdentityDbContext NewDb()
        => new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"dist-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task GetAllAsync_returns_all_distances_ordered_by_meters()
    {
        await using var db = NewDb();
        db.Distances.Add(new Distance("100m", "100m", "100 متر", 100));
        db.Distances.Add(new Distance("50m", "50m", "50 متر", 50));
        await db.SaveChangesAsync();

        var all = await new DistanceRepository(db).GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Equal(50, all[0].Meters);   // ordered by meters
        Assert.Equal(100, all[1].Meters);
    }

    [Fact]
    public async Task ExistsAsync_is_true_only_for_a_known_id()
    {
        await using var db = NewDb();
        var d = new Distance("50m", "50m", null, 50);
        db.Distances.Add(d);
        await db.SaveChangesAsync();

        var repo = new DistanceRepository(db);
        Assert.True(await repo.ExistsAsync(d.Id));
        Assert.False(await repo.ExistsAsync(Guid.NewGuid()));
    }
}
