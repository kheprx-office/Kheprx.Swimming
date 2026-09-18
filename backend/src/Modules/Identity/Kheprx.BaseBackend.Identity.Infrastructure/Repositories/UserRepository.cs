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
    // read-only copy, and user.SetPassword() + SaveChangesAsync() would save nothing.
    public Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
    }

    #endregion

    #region GetByUsernameAsync — user by username (read-only, for pre-check)

    public Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        var normalized = username.Trim();
        return _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == normalized, ct);
    }

    #endregion

    #region GetByIdAsync — user by id

    // The user with this id, or null — tracked, so it can be edited and saved.
    public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    #endregion

    #region ListAsync — users, optional name search (read-only)

    // Read-only users sorted by NameEn; optional case-insensitive name search (ILike).
    public async Task<IReadOnlyList<AppUser>> ListAsync(string? search = null, CancellationToken ct = default)
    {
        var query = _db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim()
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
            var pattern = "%" + term + "%";
            query = query.Where(u => EF.Functions.ILike(u.NameEn, pattern));
        }
        return await query.OrderBy(u => u.NameEn).ToListAsync(ct);
    }

    #endregion

    #region GetGenderCodeAsync — gender code by gender id (read-only)

    // Returns the Code of the gender row matching genderId, or null when genderId is null
    // or no matching row exists.
    public Task<string?> GetGenderCodeAsync(Guid? genderId, CancellationToken ct = default)
        => genderId is null
            ? Task.FromResult<string?>(null)
            : _db.Genders.AsNoTracking().Where(g => g.Id == genderId).Select(g => (string?)g.Code).FirstOrDefaultAsync(ct);

    #endregion

    #region AddAsync — stage a new user for insert

    // Stages a new user for insert (written on the next SaveChanges).
    public async Task AddAsync(AppUser user, CancellationToken ct = default)
        => await _db.Users.AddAsync(user, ct);

    #endregion

    #region SaveChangesAsync — commit the unit of work

    // Commits all pending inserts/updates/deletes in one transaction.
    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    #endregion

    #endregion
}
