namespace Kheprx.BaseBackend.Attendance.Application.DTOs;

/// <summary>A swimmer's attendance record. RecordedByName* are resolved in the API layer (empty from the service).</summary>
public sealed record AttendanceRecordDto(
    Guid Id,
    Guid SwimmerId,
    DateOnly SessionDate,
    Guid StatusId,
    string? CoachNoteEn,
    string? CoachNoteAr,
    Guid RecordedBy,
    string RecordedByNameEn,
    string? RecordedByNameAr);
