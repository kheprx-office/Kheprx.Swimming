using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.ReadModels;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class SwimmerProfileRepository : ISwimmerProfileRepository
{
    private readonly IdentityDbContext _db;
    public SwimmerProfileRepository(IdentityDbContext db) => _db = db;

    public Task<int> CountAsync(CancellationToken ct = default)
        => _db.SwimmerProfiles.AsNoTracking().CountAsync(ct);

    public async Task<int> GetMaxUidNumberAsync(CancellationToken ct = default)
    {
        var uids = await _db.SwimmerProfiles.AsNoTracking().Select(s => s.Uid).ToListAsync(ct);
        var max = 0;
        foreach (var uid in uids)
            if (uid.StartsWith("SW-") && int.TryParse(uid.AsSpan(3), out var n) && n > max) max = n;
        return max;
    }

    public async Task<IReadOnlyList<SwimmerListRow>> ListAsync(string? search = null, CancellationToken ct = default)
    {
        var query =
            from p in _db.SwimmerProfiles.AsNoTracking()
            join u in _db.Users.AsNoTracking() on p.UserId equals u.Id
            join c in _db.Clubs.AsNoTracking() on p.TrainingClubId equals c.Id into clubs
            from c in clubs.DefaultIfEmpty()
            join g in _db.Genders.AsNoTracking() on u.GenderId equals g.Id into genders
            from g in genders.DefaultIfEmpty()
            select new { p, u, c, g };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim()
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
            var pattern = "%" + term + "%";
            query = query.Where(x =>
                EF.Functions.ILike(x.u.NameEn, pattern) ||
                (x.u.NameAr != null && EF.Functions.ILike(x.u.NameAr, pattern)) ||
                EF.Functions.ILike(x.p.Uid, pattern));
        }

        return await query
            .OrderBy(x => x.u.NameEn)
            .Select(x => new SwimmerListRow(
                x.p.Id,
                x.p.Uid,
                x.u.NameEn,
                x.u.NameAr,
                x.c == null ? null : x.c.NameEn,
                x.c == null ? null : x.c.NameAr,
                x.g == null ? null : x.g.Code,
                x.u.Dob))
            .ToListAsync(ct);
    }

    public async Task AddAsync(SwimmerProfile profile, CancellationToken ct = default)
        => await _db.SwimmerProfiles.AddAsync(profile, ct);

    public async Task AddSpecializationsAsync(IEnumerable<SwimmerSpecialization> specializations, CancellationToken ct = default)
        => await _db.SwimmerSpecializations.AddRangeAsync(specializations, ct);

    public async Task<bool> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Unique-index race (username/email/uid/user_id). Other DbUpdateExceptions propagate (→ 500).
            return false;
        }
    }
}
