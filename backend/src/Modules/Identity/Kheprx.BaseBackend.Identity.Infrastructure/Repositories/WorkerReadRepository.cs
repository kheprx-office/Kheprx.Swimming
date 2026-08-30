using Kheprx.BaseBackend.Identity.Domain.ReadModels;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class WorkerReadRepository : IWorkerReadRepository
{
    #region Fields

    private readonly IdentityDbContext _db;

    #endregion

    #region Constructor

    public WorkerReadRepository(IdentityDbContext db)
    {
        _db = db;
    }

    #endregion

    #region APIs

    #region GetDetailAsync — single worker row by users.Id

    public async Task<WorkerDetailRow?> GetDetailAsync(Guid workerUserId, CancellationToken ct = default)
    {
        return await (
            from w in _db.Workers.AsNoTracking()
            join u in _db.Users on w.UserId equals u.Id
            where w.UserId == workerUserId
            select new WorkerDetailRow(
                w.UserId, u.FullName, u.Gender, u.Age, u.Phone, u.Nid, u.IsActive, w.HireDate))
            .FirstOrDefaultAsync(ct);
    }

    #endregion

    #region ListAsync — worker read-rows for the given ids, optional search / active filter

    public async Task<IReadOnlyList<WorkerListRow>> ListAsync(
        IReadOnlyList<Guid> workerIds, string? search, bool? isActive, CancellationToken ct = default)
    {
        if (workerIds.Count == 0) return Array.Empty<WorkerListRow>();

        // Read-only projection (AsNoTracking). The incoming ids are user ids
        // (project_workers.WorkerId → users.Id), so match workers by UserId, then join to the user.
        var query =
            from w in _db.Workers.AsNoTracking()
            join u in _db.Users on w.UserId equals u.Id
            where workerIds.Contains(w.UserId)
            select new { w, u };

        if (isActive.HasValue)
            query = query.Where(x => x.u.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var (namePattern, nidPattern) = SearchPatterns.ForNameAndNid(search);
            query = query.Where(x =>
                EF.Functions.ILike(x.u.FullName, namePattern) ||
                EF.Functions.ILike(x.u.Nid, nidPattern));
        }

        return await query
            .OrderBy(x => x.u.FullName)
            .Select(x => new WorkerListRow(
                x.w.UserId, x.u.FullName, x.u.Gender, x.u.Age, x.u.Phone, x.u.Nid,
                x.u.IsActive))
            .ToListAsync(ct);
    }

    #endregion

    #region ListAllAsync — all worker-users (no id filter), optional active filter

    public async Task<IReadOnlyList<WorkerListRow>> ListAllAsync(bool? isActive, CancellationToken ct = default)
    {
        // Read-only projection (AsNoTracking) of every worker joined to its user.
        // CRITICAL: the row Id is users.Id (w.UserId) — project_workers.WorkerId → users.Id
        // and ProjectWriteService validates against UserId. Do NOT return w.Id here.
        var query =
            from w in _db.Workers.AsNoTracking()
            join u in _db.Users on w.UserId equals u.Id
            select new { w, u };

        if (isActive.HasValue)
            query = query.Where(x => x.u.IsActive == isActive.Value);

        return await query
            .OrderBy(x => x.u.FullName)
            .Select(x => new WorkerListRow(
                x.w.UserId, x.u.FullName, x.u.Gender, x.u.Age, x.u.Phone, x.u.Nid,
                x.u.IsActive))
            .ToListAsync(ct);
    }

    #endregion

    #endregion
}
