using Kheprx.BaseBackend.Attendance.Application.DTOs;
using Kheprx.BaseBackend.Attendance.Application.Services;
using Kheprx.BaseBackend.Attendance.Domain.Entities;
using Kheprx.BaseBackend.Attendance.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Attendance.UnitTests.Services;

public class AttendanceServiceTests
{
    private static readonly Guid Present = Guid.NewGuid();
    private static readonly Guid Late = Guid.NewGuid();
    private static readonly Guid Absent = Guid.NewGuid();
    private static readonly Guid Excused = Guid.NewGuid();

    [Fact]
    public async Task ListBySwimmerAsync_maps_entities_to_dtos_with_empty_recorder_names()
    {
        var sw = Guid.NewGuid();
        var recorder = Guid.NewGuid();
        var repo = new Mock<IAttendanceRecordRepository>();
        repo.Setup(r => r.ListBySwimmerAsync(sw, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new AttendanceRecord(sw, new DateOnly(2026, 9, 9), Guid.NewGuid(), recorder, "note", null) });

        var svc = new AttendanceService(repo.Object);
        var list = await svc.ListBySwimmerAsync(sw);

        Assert.Single(list);
        Assert.Equal(sw, list[0].SwimmerId);
        Assert.Equal(recorder, list[0].RecordedBy);
        Assert.Equal("note", list[0].CoachNoteEn);
        Assert.Equal(string.Empty, list[0].RecordedByNameEn); // enriched later by the controller
        Assert.Null(list[0].RecordedByNameAr);
    }

    [Fact]
    public async Task GetMonthStatusCountsAsync_groups_counts_by_swimmer_then_status()
    {
        var swimmer = Guid.NewGuid();
        var repo = new Mock<IAttendanceRecordRepository>();
        repo.Setup(r => r.ListByMonthAsync(2026, 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new AttendanceRecord(swimmer, new DateOnly(2026, 9, 1), Present, Guid.NewGuid()),
                new AttendanceRecord(swimmer, new DateOnly(2026, 9, 2), Present, Guid.NewGuid()),
                new AttendanceRecord(swimmer, new DateOnly(2026, 9, 3), Absent,  Guid.NewGuid()),
            });
        var svc = new AttendanceService(repo.Object);

        var counts = await svc.GetMonthStatusCountsAsync(2026, 9);

        Assert.Equal(2, counts[swimmer][Present]);
        Assert.Equal(1, counts[swimmer][Absent]);
        Assert.False(counts[swimmer].ContainsKey(Late));
    }

    [Fact]
    public async Task SaveSessionAsync_maps_entries_to_records_stamped_with_recorder()
    {
        var date = new DateOnly(2026, 9, 23);
        var recorder = Guid.NewGuid();
        var swimmer = Guid.NewGuid();
        IReadOnlyList<AttendanceRecord>? captured = null;
        var repo = new Mock<IAttendanceRecordRepository>();
        repo.Setup(r => r.UpsertSessionAsync(date, It.IsAny<IReadOnlyList<AttendanceRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<DateOnly, IReadOnlyList<AttendanceRecord>, CancellationToken>((_, recs, _) => captured = recs)
            .Returns(Task.CompletedTask);
        var svc = new AttendanceService(repo.Object);

        await svc.SaveSessionAsync(date, new[]
        {
            new SaveSessionEntry(swimmer, Present, "note en", null),
        }, recorder);

        Assert.NotNull(captured);
        var rec = Assert.Single(captured!);
        Assert.Equal(swimmer, rec.SwimmerId);
        Assert.Equal(date, rec.SessionDate);
        Assert.Equal(Present, rec.StatusId);
        Assert.Equal(recorder, rec.RecordedBy);
        Assert.Equal("note en", rec.CoachNoteEn);
    }
}
