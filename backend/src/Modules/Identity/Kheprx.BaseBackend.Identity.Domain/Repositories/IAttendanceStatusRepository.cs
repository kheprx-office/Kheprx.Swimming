using Kheprx.BaseBackend.Identity.Domain.Entities;
namespace Kheprx.BaseBackend.Identity.Domain.Repositories;
public interface IAttendanceStatusRepository
{
    Task<IReadOnlyList<AttendanceStatus>> GetAllAsync(CancellationToken ct = default);
}
