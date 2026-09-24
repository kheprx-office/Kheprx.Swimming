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

    public async Task<IReadOnlyList<AttendanceRecordDto>> ListByDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var rows = await _records.ListByDateAsync(date, ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, int>>> GetMonthStatusCountsAsync(
        int year, int month, CancellationToken ct = default)
    {
        var rows = await _records.ListByMonthAsync(year, month, ct);
        return rows
            .GroupBy(r => r.SwimmerId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<Guid, int>)g.GroupBy(r => r.StatusId)
                        .ToDictionary(s => s.Key, s => s.Count()));
    }

    public async Task<IReadOnlyList<DailyStatusCountsDto>> GetRecentDailyStatusCountsAsync(
        int days, CancellationToken ct = default)
    {
        var rows = await _records.ListRecentDaysAsync(days, ct);
        return rows
            .GroupBy(r => r.SessionDate)
            .OrderBy(g => g.Key) // oldest -> newest
            .Select(g => new DailyStatusCountsDto(
                g.Key,
                (IReadOnlyDictionary<Guid, int>)g.GroupBy(r => r.StatusId)
                    .ToDictionary(s => s.Key, s => s.Count())))
            .ToList();
    }

    public async Task SaveSessionAsync(DateOnly date, IReadOnlyList<SaveSessionEntry> entries, Guid recordedBy,
        CancellationToken ct = default)
    {
        var records = entries
            .Select(e => new AttendanceRecord(e.SwimmerId, date, e.StatusId, recordedBy, e.CoachNoteEn, e.CoachNoteAr))
            .ToList();
        await _records.UpsertSessionAsync(date, records, ct);
    }

    private static AttendanceRecordDto ToDto(AttendanceRecord r) =>
        new(r.Id, r.SwimmerId, r.SessionDate, r.StatusId, r.CoachNoteEn, r.CoachNoteAr, r.RecordedBy, string.Empty, null);
}
