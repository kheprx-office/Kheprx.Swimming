using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class UserRepository : IUserRepository
{
    #region Fields

    private readonly IdentityDbContext _db;

    #endregion

    #region Constructor

    public UserRepository(IdentityDbContext db)
    {
        _db = db;
    }

    #endregion

    #region APIs

    #region GetByEmailAsync — user by email (tracked, for login)

    // This returns a TRACKED entity, on purpose: we query _db.Users directly and do
    // NOT add .AsNoTracking(). EF Core therefore remembers the object it hands back,
    // which is what lets callers (e.g. AuthService.LoginAsync) modify it and call
    // SaveChangesAsync() without passing it - EF already knows what changed.
    // If we ever wrote _db.Users.AsNoTracking()... the object would be a detached
    // read-only copy, and user.RecordLogin() + SaveChangesAsync() would save nothing.
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
    }

    #endregion

    #region GetByIdAsync — user by id

    // The user with this id, or null — tracked, so it can be edited and saved.
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    #endregion

    #region GetByNidAsync — user by national id

    // The user whose national id matches (trimmed first), or null — tracked, so it can be edited and saved.
    public Task<User?> GetByNidAsync(string nid, CancellationToken ct = default)
    {
        var normalized = nid.Trim();
        return _db.Users.FirstOrDefaultAsync(u => u.Nid == normalized, ct);
    }

    #endregion

    #region ListAsync — users, optional name/nid search (read-only)

    // Read-only users sorted by name; optional case-insensitive name/nid search (ILike).
    public async Task<IReadOnlyList<User>> ListAsync(string? search = null, CancellationToken ct = default)
    {
        var query = _db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var (namePattern, nidPattern) = SearchPatterns.ForNameAndNid(search);
            query = query.Where(u =>
                EF.Functions.ILike(u.FullName, namePattern) ||
                EF.Functions.ILike(u.Nid, nidPattern));
        }
        return await query.OrderBy(u => u.FullName).ToListAsync(ct);
    }

    #endregion

    #region ListCodesByPrefixAsync — existing codes for a prefix (read-only)

    // Read-only: all existing user codes that start with the given prefix.
    public async Task<IReadOnlyList<string>> ListCodesByPrefixAsync(string prefix, CancellationToken ct = default)
        => await _db.Users.AsNoTracking()
            .Where(u => u.Code != null && u.Code.StartsWith(prefix))
            .Select(u => u.Code!)
            .ToListAsync(ct);

    #endregion

    #region AddAsync — stage a new user for insert

    // Stages a new user for insert (written on the next SaveChanges).
    public async Task AddAsync(User user, CancellationToken ct = default)
        => await _db.Users.AddAsync(user, ct);

    #endregion

    #region SaveChangesAsync — commit the unit of work

    // Commits all pending inserts/updates/deletes in one transaction.
    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    #endregion

    #endregion
}
