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

public sealed record SwimmerSessionRowDto(
    Guid SwimmerId, string Uid, string NameEn, string? NameAr,
    string? ClubNameEn, string? ClubNameAr, string GenderCode,
    Guid? StatusId, string? CoachNote, int? MonthRatePct, bool HasRecord);

public sealed record AttendanceSessionDto(DateOnly Date, IReadOnlyList<SwimmerSessionRowDto> Rows);

public sealed record SaveSessionRequest(DateOnly Date, IReadOnlyList<SaveSessionEntryRequest> Entries);
public sealed record SaveSessionEntryRequest(Guid SwimmerId, Guid StatusId, string? CoachNote);

public sealed record SaveSessionEntry(Guid SwimmerId, Guid StatusId, string? CoachNoteEn, string? CoachNoteAr);

/// <summary>Per-date status counts (StatusId -> count) for the dashboard weekly chart.</summary>
public sealed record DailyStatusCountsDto(DateOnly Date, IReadOnlyDictionary<Guid, int> Counts);
