namespace Kheprx.BaseBackend.Attendance.Domain.Entities;

public sealed class AttendanceRecord
{
    public Guid Id { get; private set; }
    public Guid SwimmerId { get; private set; }
    public DateOnly SessionDate { get; private set; }
    public Guid StatusId { get; private set; }
    public Guid RecordedBy { get; private set; }
    public string? CoachNoteEn { get; private set; }
    public string? CoachNoteAr { get; private set; }

    private AttendanceRecord() { } // EF Core

    public AttendanceRecord(Guid swimmerId, DateOnly sessionDate, Guid statusId, Guid recordedBy,
        string? coachNoteEn = null, string? coachNoteAr = null)
    {
        Id = Guid.NewGuid();
        SwimmerId = swimmerId;
        SessionDate = sessionDate;
        StatusId = statusId;
        RecordedBy = recordedBy;
        CoachNoteEn = string.IsNullOrWhiteSpace(coachNoteEn) ? null : coachNoteEn.Trim();
        CoachNoteAr = string.IsNullOrWhiteSpace(coachNoteAr) ? null : coachNoteAr.Trim();
    }
}
