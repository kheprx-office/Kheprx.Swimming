using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Kheprx.BaseBackend.Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Repositories;

public class FitnessAssessmentRepositoryTests
{
    private static IdentityDbContext NewDb() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task GetAllAsync_returns_rows_ordered_by_name()
    {
        await using var db = NewDb();
        db.FitnessAssessments.Add(new FitnessAssessment("unfit", "Unfit", "غير لائق"));
        db.FitnessAssessments.Add(new FitnessAssessment("fit", "Fit", "لائق"));
        await db.SaveChangesAsync();

        var rows = await new FitnessAssessmentRepository(db).GetAllAsync();

        Assert.Equal(2, rows.Count);
        Assert.Equal("Fit", rows[0].NameEn); // ordered by NameEn
    }

    [Fact]
    public async Task ExistsAsync_is_true_only_for_known_ids()
    {
        await using var db = NewDb();
        var fa = new FitnessAssessment("fit", "Fit", "لائق");
        db.FitnessAssessments.Add(fa);
        await db.SaveChangesAsync();
        var repo = new FitnessAssessmentRepository(db);

        Assert.True(await repo.ExistsAsync(fa.Id));
        Assert.False(await repo.ExistsAsync(Guid.NewGuid()));
    }
}
