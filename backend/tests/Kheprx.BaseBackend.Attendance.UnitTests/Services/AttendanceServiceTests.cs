using Kheprx.BaseBackend.Attendance.Application.Services;
using Kheprx.BaseBackend.Attendance.Domain.Entities;
using Kheprx.BaseBackend.Attendance.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Attendance.UnitTests.Services;

public class AttendanceServiceTests
{
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
}
