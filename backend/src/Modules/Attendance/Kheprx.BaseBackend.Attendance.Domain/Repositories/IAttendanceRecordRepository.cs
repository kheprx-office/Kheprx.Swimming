using Kheprx.BaseBackend.Attendance.Domain.Entities;
namespace Kheprx.BaseBackend.Attendance.Domain.Repositories;
public interface IAttendanceRecordRepository
{
    Task<IReadOnlyList<AttendanceRecord>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default);
    Task<IReadOnlyList<AttendanceRecord>> ListByDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<AttendanceRecord>> ListByMonthAsync(int year, int month, CancellationToken ct = default);
    Task UpsertSessionAsync(DateOnly date, IReadOnlyList<AttendanceRecord> incoming, CancellationToken ct = default);
    Task<IReadOnlyList<AttendanceRecord>> ListRecentDaysAsync(int dayCount, CancellationToken ct = default);
}
