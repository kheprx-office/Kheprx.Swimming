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

    [Fact]
    public async Task ListByDateAsync_returns_only_that_date()
    {
        await using var db = NewDb();
        var d = new DateOnly(2026, 9, 23);
        db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), d, Guid.NewGuid(), Guid.NewGuid()));
        db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 24), Guid.NewGuid(), Guid.NewGuid()));
        await db.SaveChangesAsync();

        var list = await new AttendanceRecordRepository(db).ListByDateAsync(d);

        Assert.Single(list);
        Assert.All(list, r => Assert.Equal(d, r.SessionDate));
    }

    [Fact]
    public async Task ListByMonthAsync_returns_only_that_month()
    {
        await using var db = NewDb();
        db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 3), Guid.NewGuid(), Guid.NewGuid()));
        db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 28), Guid.NewGuid(), Guid.NewGuid()));
        db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 8, 30), Guid.NewGuid(), Guid.NewGuid()));
        await db.SaveChangesAsync();

        var list = await new AttendanceRecordRepository(db).ListByMonthAsync(2026, 9);

        Assert.Equal(2, list.Count);
        Assert.All(list, r => Assert.Equal(9, r.SessionDate.Month));
    }

    [Fact]
    public async Task ListRecentDaysAsync_returns_records_for_the_most_recent_distinct_days_only()
    {
        await using var db = NewDb();
        var st = Guid.NewGuid();
        // Four distinct dates; ask for the 3 most recent.
        db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 10), st, Guid.NewGuid()));
        db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 20), st, Guid.NewGuid()));
        db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 20), st, Guid.NewGuid())); // same day, 2 rows
        db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 25), st, Guid.NewGuid()));
        db.AttendanceRecords.Add(new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 28), st, Guid.NewGuid()));
        await db.SaveChangesAsync();

        var repo = new AttendanceRecordRepository(db);
        var rows = await repo.ListRecentDaysAsync(3);

        var days = rows.Select(r => r.SessionDate).Distinct().OrderBy(d => d).ToList();
        Assert.Equal(new[] { new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 25), new DateOnly(2026, 9, 28) }, days);
        Assert.Equal(4, rows.Count); // Sep 20 contributes 2 rows
    }

    [Fact]
    public async Task ListRecentDaysAsync_returns_empty_when_no_records()
    {
        await using var db = NewDb();
        var rows = await new AttendanceRecordRepository(db).ListRecentDaysAsync(7);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task UpsertSessionAsync_inserts_new_and_updates_existing_without_duplicating()
    {
        await using var db = NewDb();
        var d = new DateOnly(2026, 9, 23);
        var swimmerA = Guid.NewGuid();
        var swimmerB = Guid.NewGuid();
        var statusPresent = Guid.NewGuid();
        var statusAbsent = Guid.NewGuid();
        var coach = Guid.NewGuid();

        // A already has a record for the date (Present); B has none.
        db.AttendanceRecords.Add(new AttendanceRecord(swimmerA, d, statusPresent, coach));
        await db.SaveChangesAsync();

        var repo = new AttendanceRecordRepository(db);
        await repo.UpsertSessionAsync(d, new[]
        {
            new AttendanceRecord(swimmerA, d, statusAbsent, coach, "changed", null), // update A → Absent
            new AttendanceRecord(swimmerB, d, statusPresent, coach),                 // insert B
        });

        var all = await repo.ListByDateAsync(d);
        Assert.Equal(2, all.Count); // no duplicate for A
        var a = all.Single(r => r.SwimmerId == swimmerA);
        Assert.Equal(statusAbsent, a.StatusId);
        Assert.Equal("changed", a.CoachNoteEn);
        Assert.Contains(all, r => r.SwimmerId == swimmerB && r.StatusId == statusPresent);
    }
}
