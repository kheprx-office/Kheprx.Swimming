using Kheprx.BaseBackend.Identity.Contracts;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Kheprx.BaseBackend.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Kheprx.BaseBackend.Identity.Infrastructure.Services;

internal sealed class IdentityModuleApi : IIdentityModule
{
    private readonly IdentityDbContext _db;
    private readonly IWorkerReadRepository _workers;
    private readonly IManagerReadRepository _managers;
    private readonly IMoqawelReadRepository _moqaweleen;

    public IdentityModuleApi(
        IdentityDbContext db,
        IWorkerReadRepository workers,
        IManagerReadRepository managers,
        IMoqawelReadRepository moqaweleen)
    {
        _db = db;
        _workers = workers;
        _managers = managers;
        _moqaweleen = moqaweleen;
    }

    public Task<bool> UserExistsAsync(Guid userId, CancellationToken ct = default)
        => _db.Users.AnyAsync(u => u.Id == userId, ct);

    public async Task<IReadOnlyList<WorkerPersonDto>> ListWorkersAsync(
        IReadOnlyList<Guid> workerIds, string? search, bool? isActive, CancellationToken ct = default)
    {
        var rows = await _workers.ListAsync(workerIds, search, isActive, ct);
        return rows.Select(r => new WorkerPersonDto(
            r.Id, r.FullName, r.Gender, r.Age, r.Phone, r.Nid, r.IsActive)).ToList();
    }

    public async Task<WorkerDetailPersonDto?> GetWorkerDetailAsync(Guid workerId, CancellationToken ct = default)
    {
        var row = await _workers.GetDetailAsync(workerId, ct);
        return row is null
            ? null
            : new WorkerDetailPersonDto(row.Id, row.FullName, row.Gender, row.Age, row.Phone, row.Nid, row.IsActive, row.HireDate);
    }

    public async Task<IReadOnlyList<ManagerNameDto>> ListManagerNamesAsync(
        IReadOnlyList<Guid> managerIds, CancellationToken ct = default)
    {
        var rows = await _managers.ListNamesAsync(managerIds, ct);
        return rows.Select(r => new ManagerNameDto(r.Id, r.FullName)).ToList();
    }

    public async Task<IReadOnlyList<ManagerNameDto>> ListManagersAsync(CancellationToken ct = default)
    {
        var rows = await _managers.ListAllAsync(ct);
        return rows.Select(r => new ManagerNameDto(r.Id, r.FullName)).ToList();
    }

    public Task<decimal?> GetManagerMonthlySalaryAsync(Guid managerId, CancellationToken ct = default)
        => _managers.GetMonthlySalaryAsync(managerId, ct);

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetWorkerDailyWagesAsync(
        IReadOnlyList<Guid> workerUserIds, CancellationToken ct = default)
    {
        if (workerUserIds.Count == 0) return new Dictionary<Guid, decimal>();
        var wanted = workerUserIds.ToHashSet();
        var rows = await _db.Workers.AsNoTracking()
            .Where(w => wanted.Contains(w.UserId))
            .Select(w => new { w.UserId, w.DailyWage })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.UserId, r => r.DailyWage);
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetMoqawelDailyWagesAsync(
        IReadOnlyList<Guid> moqawelUserIds, CancellationToken ct = default)
    {
        if (moqawelUserIds.Count == 0) return new Dictionary<Guid, decimal>();
        var wanted = moqawelUserIds.ToHashSet();
        var rows = await _db.Moqaweleen.AsNoTracking()
            .Where(m => wanted.Contains(m.UserId))
            .Select(m => new { m.UserId, m.DailyWage })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.UserId, r => r.DailyWage);
    }

    public async Task<IReadOnlyList<MoqawelPersonDto>> ListMoqaweleenAsync(
        IReadOnlyList<Guid> moqawelIds, string? search, bool? isActive, CancellationToken ct = default)
    {
        var rows = await _moqaweleen.ListAsync(moqawelIds, search, isActive, ct);
        return rows.Select(r => new MoqawelPersonDto(
            r.Id, r.FullName, r.Gender, r.Age, r.Phone, r.Nid, r.IsActive)).ToList();
    }

    public async Task<IReadOnlyList<PersonOptionDto>> ListAvailableMoqaweleenAsync(CancellationToken ct = default)
    {
        var rows = await _moqaweleen.ListAllAsync(true, ct);
        return rows.Select(r => new PersonOptionDto(r.Id, r.FullName)).ToList();
    }

    public async Task<IReadOnlyList<PersonOptionDto>> ListAvailableWorkersAsync(CancellationToken ct = default)
    {
        var rows = await _workers.ListAllAsync(true, ct);
        return rows.Select(r => new PersonOptionDto(r.Id, r.FullName)).ToList();
    }

    public async Task<Guid?> GetManagerUserIdAsync(Guid managerId, CancellationToken ct = default)
    {
        var ids = await _db.Managers.AsNoTracking()
            .Where(m => m.Id == managerId)
            .Select(m => (Guid?)m.UserId)
            .ToListAsync(ct);
        return ids.Count == 0 ? null : ids[0];
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(
        IReadOnlyList<Guid> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, string>();
        var wanted = userIds.ToHashSet();
        var rows = await _db.Users.AsNoTracking()
            .Where(u => wanted.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Id, r => r.FullName);
    }
}
