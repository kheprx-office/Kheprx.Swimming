using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Infrastructure.Data;
using Kheprx.BaseBackend.Health.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Repositories;

public class MedicalTestRepositoryTests
{
    private static HealthDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<HealthDbContext>()
            .UseInMemoryDatabase($"health-{Guid.NewGuid()}")
            .Options;
        return new HealthDbContext(options);
    }

    [Fact]
    public async Task Add_then_List_returns_saved_rows_ordered_by_name()
    {
        await using var db = NewDb();
        var repo = new MedicalTestRepository(db);
        await repo.AddAsync(new MedicalTest("Uric Acid", "حمض اليوريك", "mg/dL", 1m, 7m, Guid.NewGuid()));
        await repo.AddAsync(new MedicalTest("Glucose", "الجلوكوز", "mg/dL", 70m, 110m, Guid.NewGuid()));
        await repo.SaveChangesAsync();

        var list = await repo.ListAsync();

        Assert.Equal(2, list.Count);
        Assert.Equal("Glucose", list[0].NameEn); // alphabetical
        Assert.Equal("Uric Acid", list[1].NameEn);
    }

    [Fact]
    public async Task GetById_returns_row_then_Remove_deletes_it()
    {
        await using var db = NewDb();
        var repo = new MedicalTestRepository(db);
        var test = new MedicalTest("Hemoglobin", "الهيموغلوبين", "g/dL", 11m, 17.5m, Guid.NewGuid());
        await repo.AddAsync(test);
        await repo.SaveChangesAsync();

        var found = await repo.GetByIdAsync(test.Id);
        Assert.NotNull(found);

        await repo.RemoveAsync(found!);
        await repo.SaveChangesAsync();

        Assert.Null(await repo.GetByIdAsync(test.Id));
    }
}
