using Kheprx.BaseBackend.Attendance.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Attendance.UnitTests.Entities;

public class AttendanceRecordTests
{
    [Fact]
    public void Constructor_assigns_fields_and_generates_id()
    {
        var swimmer = Guid.NewGuid(); var status = Guid.NewGuid(); var coach = Guid.NewGuid();
        var r = new AttendanceRecord(swimmer, new DateOnly(2026, 9, 9), status, coach, "note", "ملاحظة");
        Assert.NotEqual(Guid.Empty, r.Id);
        Assert.Equal(swimmer, r.SwimmerId);
        Assert.Equal(new DateOnly(2026, 9, 9), r.SessionDate);
        Assert.Equal(status, r.StatusId);
        Assert.Equal(coach, r.RecordedBy);
        Assert.Equal("note", r.CoachNoteEn);
        Assert.Equal("ملاحظة", r.CoachNoteAr);
    }

    [Fact]
    public void Constructor_nulls_blank_notes()
    {
        var r = new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 9), Guid.NewGuid(), Guid.NewGuid(), "  ", null);
        Assert.Null(r.CoachNoteEn);
        Assert.Null(r.CoachNoteAr);
    }

    [Fact]
    public void Update_overwrites_status_recorder_and_trims_notes()
    {
        var rec = new AttendanceRecord(Guid.NewGuid(), new DateOnly(2026, 9, 23),
            Guid.NewGuid(), Guid.NewGuid(), "old", "قديم");
        var newStatus = Guid.NewGuid();
        var newRecorder = Guid.NewGuid();

        rec.Update(newStatus, newRecorder, "  new note  ", "   ");

        Assert.Equal(newStatus, rec.StatusId);
        Assert.Equal(newRecorder, rec.RecordedBy);
        Assert.Equal("new note", rec.CoachNoteEn); // trimmed
        Assert.Null(rec.CoachNoteAr);              // whitespace → null
    }
}
