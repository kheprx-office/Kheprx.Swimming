using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Repositories;

internal sealed class UserProfileRepository : IUserProfileRepository
{
    #region Fields

    private readonly IdentityDbContext _db;

    #endregion

    #region Constructor

    public UserProfileRepository(IdentityDbContext db)
    {
        _db = db;
    }

    #endregion

    #region APIs

    #region GetManagerAsync — manager profile by user id

    // The manager profile for this user, or null — tracked, so it can be edited and saved.
    public Task<Manager?> GetManagerAsync(Guid userId, CancellationToken ct = default)
        => _db.Managers.FirstOrDefaultAsync(m => m.UserId == userId, ct);

    #endregion

    #region GetMoqawelAsync — moqawel profile by user id

    // The moqawel profile for this user, or null — tracked, so it can be edited and saved.
    public Task<Moqawel?> GetMoqawelAsync(Guid userId, CancellationToken ct = default)
        => _db.Moqaweleen.FirstOrDefaultAsync(m => m.UserId == userId, ct);

    #endregion

    #region GetWorkerAsync — worker profile by user id

    // The worker profile for this user, or null — tracked, so it can be edited and saved.
    public Task<Worker?> GetWorkerAsync(Guid userId, CancellationToken ct = default)
        => _db.Workers.FirstOrDefaultAsync(w => w.UserId == userId, ct);

    #endregion

    #region AddManagerAsync — stage a new manager profile for insert

    // Stages a new manager profile for insert (written on the next SaveChanges).
    public async Task AddManagerAsync(Manager manager, CancellationToken ct = default)
        => await _db.Managers.AddAsync(manager, ct);

    #endregion

    #region AddMoqawelAsync — stage a new moqawel profile for insert

    // Stages a new moqawel profile for insert (written on the next SaveChanges).
    public async Task AddMoqawelAsync(Moqawel moqawel, CancellationToken ct = default)
        => await _db.Moqaweleen.AddAsync(moqawel, ct);

    #endregion

    #region AddWorkerAsync — stage a new worker profile for insert

    // Stages a new worker profile for insert (written on the next SaveChanges).
    public async Task AddWorkerAsync(Worker worker, CancellationToken ct = default)
        => await _db.Workers.AddAsync(worker, ct);

    #endregion

    #region ListManagersAsync — all manager profiles keyed by user id (read-only)

    // Read-only: every manager loaded into a { UserId → Manager } dictionary.
    public async Task<IReadOnlyDictionary<Guid, Manager>> ListManagersAsync(CancellationToken ct = default)
        => await _db.Managers.AsNoTracking().ToDictionaryAsync(m => m.UserId, ct);

    #endregion

    #region ListMoqaweleenAsync — all moqawel profiles keyed by user id (read-only)

    // Read-only: every moqawel loaded into a { UserId → Moqawel } dictionary.
    public async Task<IReadOnlyDictionary<Guid, Moqawel>> ListMoqaweleenAsync(CancellationToken ct = default)
        => await _db.Moqaweleen.AsNoTracking().ToDictionaryAsync(m => m.UserId, ct);

    #endregion

    #region ListWorkersAsync — all worker profiles keyed by user id (read-only)

    // Read-only: every worker loaded into a { UserId → Worker } dictionary.
    public async Task<IReadOnlyDictionary<Guid, Worker>> ListWorkersAsync(CancellationToken ct = default)
        => await _db.Workers.AsNoTracking().ToDictionaryAsync(w => w.UserId, ct);

    #endregion

    #endregion
}
