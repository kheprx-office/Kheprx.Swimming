using Kheprx.BaseBackend.Attendance.Application.DTOs;
using Kheprx.BaseBackend.Attendance.Application.Services.Interfaces;
using Kheprx.BaseBackend.Attendance.Domain.Entities;
using Kheprx.BaseBackend.Attendance.Domain.Repositories;

namespace Kheprx.BaseBackend.Attendance.Application.Services;

internal sealed class AttendanceService : IAttendanceService
{
    private readonly IAttendanceRecordRepository _records;
    public AttendanceService(IAttendanceRecordRepository records) => _records = records;

    public async Task<IReadOnlyList<AttendanceRecordDto>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default)
    {
        var rows = await _records.ListBySwimmerAsync(swimmerId, ct);
        return rows.Select(ToDto).ToList();
    }

    private static AttendanceRecordDto ToDto(AttendanceRecord r) =>
        new(r.Id, r.SwimmerId, r.SessionDate, r.StatusId, r.CoachNoteEn, r.CoachNoteAr, r.RecordedBy, string.Empty, null);
}
