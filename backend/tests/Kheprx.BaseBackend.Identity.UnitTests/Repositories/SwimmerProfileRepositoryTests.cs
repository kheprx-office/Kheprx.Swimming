using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.ReadModels;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Kheprx.BaseBackend.Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Repositories;

public class SwimmerProfileRepositoryTests
{
    private static IdentityDbContext NewDb() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task GetMaxUidNumber_is_zero_when_empty_and_parses_suffix()
    {
        await using var db = NewDb();
        var repo = new SwimmerProfileRepository(db);
        Assert.Equal(0, await repo.GetMaxUidNumberAsync());

        await repo.AddAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0003", Guid.NewGuid()));
        await repo.AddAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0011", Guid.NewGuid()));
        await repo.SaveChangesAsync();

        Assert.Equal(11, await repo.GetMaxUidNumberAsync());
    }

    [Fact]
    public async Task AddSpecializations_persists_rows()
    {
        await using var db = NewDb();
        var repo = new SwimmerProfileRepository(db);
        var swimmerId = Guid.NewGuid();
        await repo.AddSpecializationsAsync(new[]
        {
            new SwimmerSpecialization(swimmerId, Guid.NewGuid()),
            new SwimmerSpecialization(swimmerId, Guid.NewGuid()),
        });
        await repo.SaveChangesAsync();

        Assert.Equal(2, await db.SwimmerSpecializations.CountAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_returns_true_on_successful_save()
    {
        await using var db = NewDb();
        var repo = new SwimmerProfileRepository(db);
        await repo.AddAsync(new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid()));

        var result = await repo.SaveChangesAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task ListAsync_joins_user_club_gender_and_orders_by_name()
    {
        await using var db = NewDb();

        var gender = new Gender("male", "Male", "ذكر");
        var club = new Club("Oasis Main", "الواحة");
        db.Genders.Add(gender);
        db.Clubs.Add(club);

        var userB = new AppUser("b.user", "Bravo", Guid.NewGuid(), genderId: gender.Id, dob: new DateOnly(2010, 1, 1));
        var userA = new AppUser("a.user", "Alpha", Guid.NewGuid(), genderId: gender.Id, dob: new DateOnly(2012, 6, 1));
        db.Users.AddRange(userA, userB);

        db.SwimmerProfiles.Add(new SwimmerProfile(userB.Id, "SW-0002", club.Id));
        db.SwimmerProfiles.Add(new SwimmerProfile(userA.Id, "SW-0001", club.Id));
        await db.SaveChangesAsync();

        var repo = new SwimmerProfileRepository(db);
        var rows = await repo.ListAsync(null);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Alpha", rows[0].NameEn);            // ordered by NameEn
        Assert.Equal("Bravo", rows[1].NameEn);
        Assert.Equal("SW-0001", rows[0].Uid);
        Assert.Equal("Oasis Main", rows[0].ClubNameEn);
        Assert.Equal("male", rows[0].GenderCode);
        Assert.Equal(new DateOnly(2012, 6, 1), rows[0].Dob);
    }
}
