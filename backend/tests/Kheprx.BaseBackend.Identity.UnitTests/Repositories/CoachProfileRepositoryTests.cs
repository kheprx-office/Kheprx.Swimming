using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Kheprx.BaseBackend.Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Repositories;

public class CoachProfileRepositoryTests
{
    private static IdentityDbContext NewDb() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task NationalIdExists_checks_both_profile_tables()
    {
        await using var db = NewDb();
        var repo = new CoachProfileRepository(db);
        Assert.False(await repo.NationalIdExistsAsync("29001011234567"));

        await repo.AddCaptainAsync(new CaptainProfile(Guid.NewGuid(), "29001011234567"));
        await repo.AddHeadCoachAsync(new HeadCoachProfile(Guid.NewGuid(), "28502021234567"));
        var saved = await repo.SaveChangesAsync();

        Assert.True(saved);
        Assert.True(await repo.NationalIdExistsAsync("29001011234567")); // captain table
        Assert.True(await repo.NationalIdExistsAsync("28502021234567")); // head_coach table
        Assert.False(await repo.NationalIdExistsAsync("00000000000000"));
    }
}
