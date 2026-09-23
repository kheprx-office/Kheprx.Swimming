using Kheprx.BaseBackend.Championships.Domain.Entities;
using Kheprx.BaseBackend.Championships.Infrastructure.Data;
using Kheprx.BaseBackend.Championships.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Repositories;

public class CompetitionEventRepositoryTests
{
    private static ChampionshipsDbContext NewDb()
        => new(new DbContextOptionsBuilder<ChampionshipsDbContext>().UseInMemoryDatabase($"champ-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task ListAsync_returns_all_events_newest_start_first()
    {
        await using var db = NewDb();
        var sid = Guid.NewGuid();
        var cid = Guid.NewGuid();
        db.CompetitionEvents.Add(new CompetitionEvent("Oct", null, new DateOnly(2023, 10, 20), new DateOnly(2023, 10, 20), "Alex", null, sid, cid));
        db.CompetitionEvents.Add(new CompetitionEvent("Jan", null, new DateOnly(2024, 1, 20), new DateOnly(2024, 1, 21), "Giza", null, sid, cid));
        db.CompetitionEvents.Add(new CompetitionEvent("Nov", null, new DateOnly(2023, 11, 15), new DateOnly(2023, 11, 16), "Cairo", null, sid, cid));
        await db.SaveChangesAsync();

        var list = await new CompetitionEventRepository(db).ListAsync();

        Assert.Equal(3, list.Count);
        Assert.Equal("Jan", list[0].NameEn);   // 2024-01-20 newest
        Assert.Equal("Nov", list[1].NameEn);
        Assert.Equal("Oct", list[2].NameEn);
    }

    [Fact]
    public async Task AddAsync_persists_a_new_event()
    {
        await using var db = NewDb();
        var repo = new CompetitionEventRepository(db);
        var sid = Guid.NewGuid();
        var cid = Guid.NewGuid();

        await repo.AddAsync(new CompetitionEvent("Spring Cup", null, new DateOnly(2024, 3, 1),
            new DateOnly(2024, 3, 2), "Cairo", null, sid, cid));

        var list = await repo.ListAsync();
        Assert.Single(list);
        Assert.Equal("Spring Cup", list[0].NameEn);
        Assert.Equal(sid, list[0].StatusId);
        Assert.Equal(cid, list[0].CreatedBy);
    }
}
