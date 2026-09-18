using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class RoleRepository : IRoleRepository
{
    #region Fields

    private readonly IdentityDbContext _db;

    #endregion

    #region Constructor

    public RoleRepository(IdentityDbContext db)
    {
        _db = db;
    }

    #endregion

    #region APIs

    #region GetByIdAsync — role by id (read-only)

    // Read-only: the role with this id, or null.
    public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    #endregion

    #region GetByCodeAsync — role by code (tracked, for assignment)

    // The role with this code, or null — tracked (no AsNoTracking) so the caller can assign it.
    public Task<Role?> GetByCodeAsync(string code, CancellationToken ct = default)
        => _db.Roles.FirstOrDefaultAsync(r => r.Code == code, ct);

    #endregion

    #region GetAllAsync — all roles, ordered for display

    // Read-only list of all roles, sorted by NameEn.
    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default)
        => await _db.Roles.AsNoTracking().OrderBy(r => r.NameEn).ToListAsync(ct);

    #endregion

    #endregion
}
