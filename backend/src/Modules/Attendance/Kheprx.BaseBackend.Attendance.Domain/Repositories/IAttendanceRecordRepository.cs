using Kheprx.BaseBackend.Attendance.Domain.Entities;
namespace Kheprx.BaseBackend.Attendance.Domain.Repositories;
public interface IAttendanceRecordRepository
{
    Task<IReadOnlyList<AttendanceRecord>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default);
}
