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
}
