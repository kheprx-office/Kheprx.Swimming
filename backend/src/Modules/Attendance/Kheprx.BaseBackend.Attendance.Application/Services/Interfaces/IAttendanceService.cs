using Kheprx.BaseBackend.Attendance.Application.DTOs;
namespace Kheprx.BaseBackend.Attendance.Application.Services.Interfaces;
public interface IAttendanceService
{
    Task<IReadOnlyList<AttendanceRecordDto>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default);
}
