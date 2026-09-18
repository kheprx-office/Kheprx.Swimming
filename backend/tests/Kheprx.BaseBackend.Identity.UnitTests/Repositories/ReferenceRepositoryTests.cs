using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Kheprx.BaseBackend.Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Repositories;

public class ReferenceRepositoryTests
{
    private static IdentityDbContext NewDb() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task StrokeRepository_returns_all_ordered_by_name_en()
    {
        await using var db = NewDb();
        db.Strokes.AddRange(new Stroke("butterfly", "Butterfly"), new Stroke("backstroke", "Backstroke"));
        await db.SaveChangesAsync();

        var result = await new StrokeRepository(db).GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Backstroke", result[0].NameEn);
    }

    [Fact]
    public async Task BloodTypeRepository_returns_all_ordered_by_code()
    {
        await using var db = NewDb();
        db.BloodTypes.AddRange(new BloodType("O+", "O+"), new BloodType("A+", "A+"));
        await db.SaveChangesAsync();

        var result = await new BloodTypeRepository(db).GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("A+", result[0].Code);
    }

    [Fact]
    public async Task ClubRepository_returns_all_ordered_by_name_en()
    {
        await using var db = NewDb();
        db.Clubs.AddRange(new Club("Zamalek"), new Club("Al Ahly"));
        await db.SaveChangesAsync();

        var result = await new ClubRepository(db).GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Al Ahly", result[0].NameEn);
    }

    [Fact]
    public async Task GenderRepository_returns_all_ordered_by_name_en()
    {
        await using var db = NewDb();
        db.Genders.AddRange(new Gender("male", "Male"), new Gender("female", "Female"));
        await db.SaveChangesAsync();

        var result = await new GenderRepository(db).GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Female", result[0].NameEn);
    }

    [Fact]
    public async Task ObservationCategoryRepository_returns_all_ordered_by_code()
    {
        await using var db = NewDb();
        db.ObservationCategories.AddRange(
            new ObservationCategory("surgery", "Surgery", "جراحة"),
            new ObservationCategory("allergy", "Allergy", "حساسية"));
        await db.SaveChangesAsync();

        var result = await new ObservationCategoryRepository(db).GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("allergy", result[0].Code);
    }
}
