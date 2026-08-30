using Kheprx.BaseBackend.Identity.Domain.ReadModels;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class ManagerReadRepository : IManagerReadRepository
{
    private readonly IdentityDbContext _db;

    public ManagerReadRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<ManagerNameRow>> ListNamesAsync(
        IReadOnlyList<Guid> managerIds, CancellationToken ct = default)
    {
        if (managerIds.Count == 0) return Array.Empty<ManagerNameRow>();

        var query =
            from m in _db.Managers.AsNoTracking()
            join u in _db.Users on m.UserId equals u.Id
            where managerIds.Contains(m.Id)
            select new ManagerNameRow(m.Id, u.FullName);
        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ManagerNameRow>> ListAllAsync(CancellationToken ct = default)
    {
        var query =
            from m in _db.Managers.AsNoTracking()
            join u in _db.Users on m.UserId equals u.Id
            orderby u.FullName
            select new ManagerNameRow(m.Id, u.FullName);
        return await query.ToListAsync(ct);
    }

    public async Task<decimal?> GetMonthlySalaryAsync(Guid managerId, CancellationToken ct = default)
    {
        var salaries = await _db.Managers.AsNoTracking()
            .Where(m => m.Id == managerId)
            .Select(m => (decimal?)m.MonthlySalary)
            .ToListAsync(ct);
        return salaries.Count == 0 ? null : salaries[0];
    }
}
