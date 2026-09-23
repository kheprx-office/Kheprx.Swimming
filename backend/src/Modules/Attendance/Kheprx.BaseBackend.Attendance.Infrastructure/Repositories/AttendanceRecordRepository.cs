using Kheprx.BaseBackend.Attendance.Domain.Entities;
using Kheprx.BaseBackend.Attendance.Domain.Repositories;
using Kheprx.BaseBackend.Attendance.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Attendance.Infrastructure.Repositories;

internal sealed class AttendanceRecordRepository : IAttendanceRecordRepository
{
    private readonly AttendanceDbContext _db;
    public AttendanceRecordRepository(AttendanceDbContext db) => _db = db;

    public async Task<IReadOnlyList<AttendanceRecord>> ListBySwimmerAsync(Guid swimmerId, CancellationToken ct = default)
        => await _db.AttendanceRecords.AsNoTracking()
              .Where(r => r.SwimmerId == swimmerId)
              .OrderByDescending(r => r.SessionDate).ThenByDescending(r => r.Id)
              .ToListAsync(ct);
}
