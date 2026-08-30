using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    #region Fields

    private readonly IdentityDbContext _db;

    #endregion

    #region Constructor

    public RefreshTokenRepository(IdentityDbContext db)
    {
        _db = db;
    }

    #endregion

    #region APIs

    #region GetByHashAsync — refresh token by hash

    // The refresh token with this hash, or null — tracked, so it can be revoked and saved.
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        => _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    #endregion

    #region AddAsync — stage a new refresh token for insert

    // Stages a new refresh token for insert (written on the next SaveChanges).
    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
        => await _db.RefreshTokens.AddAsync(token, ct);

    #endregion

    #region RevokeAllForUserAsync — revoke every active token for a user

    // Loads the user's still-active tokens and revokes each (committed by the caller's SaveChanges).
    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var token in active)
            token.Revoke();
    }

    #endregion

    #region SaveChangesAsync — commit the unit of work

    // Commits all pending inserts/updates/deletes in one transaction.
    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    #endregion

    #endregion
}
