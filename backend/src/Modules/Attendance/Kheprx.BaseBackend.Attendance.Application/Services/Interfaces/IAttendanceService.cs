using Kheprx.BaseBackend.Attendance.Application.DTOs;
namespace Kheprx.BaseBackend.Attendance.Application.Services.Interfaces;
public interface IAttendanceService
{
    Task<IReadOnlyList<AttendanceRecordDto>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default);
    Task<IReadOnlyList<AttendanceRecordDto>> ListByDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, int>>> GetMonthStatusCountsAsync(int year, int month, CancellationToken ct = default);
    Task SaveSessionAsync(DateOnly date, IReadOnlyList<SaveSessionEntry> entries, Guid recordedBy, CancellationToken ct = default);
}
