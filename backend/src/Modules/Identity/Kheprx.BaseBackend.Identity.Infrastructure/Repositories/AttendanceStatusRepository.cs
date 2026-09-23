using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class AttendanceStatusRepository : IAttendanceStatusRepository
{
    private readonly IdentityDbContext _db;
    public AttendanceStatusRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<AttendanceStatus>> GetAllAsync(CancellationToken ct = default)
        => await _db.AttendanceStatuses.AsNoTracking().OrderBy(s => s.Code).ToListAsync(ct);
}
