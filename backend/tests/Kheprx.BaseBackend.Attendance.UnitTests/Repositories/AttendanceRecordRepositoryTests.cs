using Kheprx.BaseBackend.Attendance.Domain.Entities;
using Kheprx.BaseBackend.Attendance.Infrastructure.Data;
using Kheprx.BaseBackend.Attendance.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kheprx.BaseBackend.Attendance.UnitTests.Repositories;

public class AttendanceRecordRepositoryTests
{
    private static AttendanceDbContext NewDb()
        => new(new DbContextOptionsBuilder<AttendanceDbContext>().UseInMemoryDatabase($"att-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task ListBySwimmerAsync_returns_only_that_swimmer_newest_first()
    {
        await using var db = NewDb();
        var sw = Guid.NewGuid();
        db.AttendanceRecords.Add(new AttendanceRecord(sw, new DateOnly(2026, 9, 1), Guid.NewGuid(), Guid.NewGuid()));
        db.AttendanceRecords.Add(new AttendanceRecord(sw, new DateOnly(2026, 9, 5), Guid.NewGuid(), Guid.NewGuid()));
        db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 3), Guid.NewGuid(), Guid.NewGuid()));
        await db.SaveChangesAsync();

        var repo = new AttendanceRecordRepository(db);
        var list = await repo.ListBySwimmerAsync(sw);

        Assert.Equal(2, list.Count);
        Assert.All(list, r => Assert.Equal(sw, r.SwimmerId));
        Assert.Equal(new DateOnly(2026, 9, 5), list[0].SessionDate); // newest first
    }
}
