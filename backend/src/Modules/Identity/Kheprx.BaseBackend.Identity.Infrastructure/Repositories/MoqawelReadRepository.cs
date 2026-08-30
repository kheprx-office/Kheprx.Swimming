using Kheprx.BaseBackend.Identity.Domain.ReadModels;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class MoqawelReadRepository : IMoqawelReadRepository
{
    #region Fields

    private readonly IdentityDbContext _db;

    #endregion

    #region Constructor

    public MoqawelReadRepository(IdentityDbContext db)
    {
        _db = db;
    }

    #endregion

    #region APIs

    #region ListAsync — moqawel read-rows for the given ids, optional search / active filter

    public async Task<IReadOnlyList<MoqawelListRow>> ListAsync(
        IReadOnlyList<Guid> moqawelIds, string? search, bool? isActive, CancellationToken ct = default)
    {
        if (moqawelIds.Count == 0) return Array.Empty<MoqawelListRow>();

        // Read-only projection (AsNoTracking). The incoming ids are user ids
        // (project_moqaweleen.MoqawelId → users.Id), so match moqaweleen by UserId, then join to the user.
        var query =
            from m in _db.Moqaweleen.AsNoTracking()
            join u in _db.Users on m.UserId equals u.Id
            where moqawelIds.Contains(m.UserId)
            select new { m, u };

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
            .Select(x => new MoqawelListRow(
                x.m.UserId, x.u.FullName, x.u.Gender, x.u.Age, x.u.Phone, x.u.Nid,
                x.u.IsActive))
            .ToListAsync(ct);
    }

    #endregion

    #region ListAllAsync — all moqawel-users (no id filter), optional active filter

    public async Task<IReadOnlyList<MoqawelListRow>> ListAllAsync(bool? isActive, CancellationToken ct = default)
    {
        // Read-only projection (AsNoTracking) of every moqawel joined to its user.
        // CRITICAL: the row Id is users.Id (m.UserId) — project_moqaweleen.MoqawelId → users.Id
        // and ProjectWriteService validates against UserId. Do NOT return m.Id here.
        var query =
            from m in _db.Moqaweleen.AsNoTracking()
            join u in _db.Users on m.UserId equals u.Id
            select new { m, u };

        if (isActive.HasValue)
            query = query.Where(x => x.u.IsActive == isActive.Value);

        return await query
            .OrderBy(x => x.u.FullName)
            .Select(x => new MoqawelListRow(
                x.m.UserId, x.u.FullName, x.u.Gender, x.u.Age, x.u.Phone, x.u.Nid,
                x.u.IsActive))
            .ToListAsync(ct);
    }

    #endregion

    #endregion
}
