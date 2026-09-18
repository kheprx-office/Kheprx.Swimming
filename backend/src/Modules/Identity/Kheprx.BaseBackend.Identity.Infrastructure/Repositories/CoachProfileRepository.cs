using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class CoachProfileRepository : ICoachProfileRepository
{
    private readonly IdentityDbContext _db;
    public CoachProfileRepository(IdentityDbContext db) => _db = db;

    public Task<string?> GetNationalIdAsync(Guid userId, string roleCode, CancellationToken ct = default) => roleCode switch
    {
        "head_coach" => _db.HeadCoachProfiles.AsNoTracking()
            .Where(p => p.UserId == userId).Select(p => (string?)p.NationalId).FirstOrDefaultAsync(ct),
        "captain" => _db.CaptainProfiles.AsNoTracking()
            .Where(p => p.UserId == userId).Select(p => (string?)p.NationalId).FirstOrDefaultAsync(ct),
        _ => Task.FromResult<string?>(null),
    };

    public async Task AddHeadCoachAsync(HeadCoachProfile profile, CancellationToken ct = default)
        => await _db.HeadCoachProfiles.AddAsync(profile, ct);

    public async Task AddCaptainAsync(CaptainProfile profile, CancellationToken ct = default)
        => await _db.CaptainProfiles.AddAsync(profile, ct);

    public async Task<bool> NationalIdExistsAsync(string nationalId, CancellationToken ct = default)
        => await _db.CaptainProfiles.AsNoTracking().AnyAsync(p => p.NationalId == nationalId, ct)
        || await _db.HeadCoachProfiles.AsNoTracking().AnyAsync(p => p.NationalId == nationalId, ct);

    public async Task<bool> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Unique-index race (national_id / username / email / user_id). Other failures propagate (→ 500).
            return false;
        }
    }
}
