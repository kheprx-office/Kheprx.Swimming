using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Kheprx.BaseBackend.Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Repositories;

public class FeedbackCategoryRepositoryTests
{
    private static IdentityDbContext NewDb()
        => new(new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase($"id-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task GetAllAsync_returns_rows_ordered_by_code()
    {
        await using var db = NewDb();
        db.FeedbackCategories.Add(new FeedbackCategory("technique", "Technique", "تكنيك"));
        db.FeedbackCategories.Add(new FeedbackCategory("attitude", "Attitude", "سلوك"));
        await db.SaveChangesAsync();

        var repo = new FeedbackCategoryRepository(db);
        var all = await repo.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Equal("attitude", all[0].Code); // alphabetical
    }
}
