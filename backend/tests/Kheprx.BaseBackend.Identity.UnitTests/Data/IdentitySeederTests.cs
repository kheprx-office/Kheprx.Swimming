using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Kheprx.BaseBackend.Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Data;

public class IdentitySeederTests
{
    private static IdentityDbContext NewDb() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Seeding_twice_creates_each_row_once()
    {
        var hasher = new PasswordHasher();
        await using var db = NewDb();
        await IdentitySeeder.SeedAsync(db, hasher);
        await IdentitySeeder.SeedAsync(db, hasher);

        Assert.Equal(3, await db.Roles.CountAsync());
        Assert.Equal(2, await db.Genders.CountAsync());
        Assert.Equal(26, await db.Users.CountAsync());
        Assert.Equal(1, await db.HeadCoachProfiles.CountAsync());
        Assert.Equal(1, await db.CaptainProfiles.CountAsync());
    }

    [Fact]
    public async Task Seeding_creates_reference_lookups_once()
    {
        var hasher = new PasswordHasher();
        await using var db = NewDb();
        await IdentitySeeder.SeedAsync(db, hasher);
        await IdentitySeeder.SeedAsync(db, hasher);

        Assert.Equal(5, await db.Strokes.CountAsync());
        Assert.Equal(8, await db.BloodTypes.CountAsync());
        Assert.Equal(70, await db.Clubs.CountAsync());
    }

    [Fact]
    public async Task Seeding_creates_swimmer_role_and_linked_swimmers()
    {
        var hasher = new PasswordHasher();
        await using var db = NewDb();
        await IdentitySeeder.SeedAsync(db, hasher);
        await IdentitySeeder.SeedAsync(db, hasher);

        var swimmerRole = await db.Roles.FirstOrDefaultAsync(r => r.Code == "swimmer");
        Assert.NotNull(swimmerRole);
        Assert.Equal(24, await db.SwimmerProfiles.CountAsync());
        // every swimmer profile is linked to an app_user with the swimmer role
        var swimmerUserIds = db.Users.Where(u => u.RoleId == swimmerRole!.Id).Select(u => u.Id).ToList();
        Assert.Equal(24, swimmerUserIds.Count);
        Assert.True(db.SwimmerProfiles.All(p => swimmerUserIds.Contains(p.UserId)));
    }
}
