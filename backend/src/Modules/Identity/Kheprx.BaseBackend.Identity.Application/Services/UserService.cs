using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Exceptions;
using Kheprx.BaseBackend.Identity.Domain.Repositories;

namespace Kheprx.BaseBackend.Identity.Application.Services;

internal sealed class UserService : IUserService
{
    #region Fields

    private static readonly IReadOnlyDictionary<string, string> CodePrefixes =
        new Dictionary<string, string>
        {
            ["admin"] = "OWNER",
            ["manager"] = "PM",
            ["moqawel"] = "M",
            ["worker"] = "W",
        };

    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IUserProfileRepository _profiles;
    private readonly IPasswordHasher _hasher;

    #endregion

    #region Constructor

    public UserService(
        IUserRepository users, IRoleRepository roles,
        IUserProfileRepository profiles, IPasswordHasher hasher)
    {
        _users = users;
        _roles = roles;
        _profiles = profiles;
        _hasher = hasher;
    }

    #endregion

    #region APIs

    #region ListAsync — list users with profiles, optional search

    public async Task<IReadOnlyList<UserDto>> ListAsync(string? search = null, CancellationToken ct = default)
    {
        var users = await _users.ListAsync(search, ct);
        var roles = await _roles.GetAllAsync(ct);
        var codeById = roles.ToDictionary(r => r.Id, r => r.Code);
        var managers = await _profiles.ListManagersAsync(ct);
        var moqaweleen = await _profiles.ListMoqaweleenAsync(ct);
        var workers = await _profiles.ListWorkersAsync(ct);

        return users.Select(u =>
        {
            var roleCode = codeById.TryGetValue(u.RoleId, out var code) ? code : string.Empty;
            var profile = roleCode switch
            {
                "manager" => managers.TryGetValue(u.Id, out var m) ? ManagerProfile(m) : null,
                "moqawel" => moqaweleen.TryGetValue(u.Id, out var q) ? MoqawelProfile(q) : null,
                "worker" => workers.TryGetValue(u.Id, out var w) ? WorkerProfile(w) : null,
                _ => null,
            };
            return Map(u, roleCode, profile);
        }).ToList();
    }

    #endregion

    #region CreateAsync — create user + role profile atomically

    public async Task<UserDto?> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var role = await _roles.GetByCodeAsync(request.Role, ct)
            ?? throw new InvalidUserException($"Unknown role '{request.Role}'.");

        var nid = request.Nid.Trim();
        if (await _users.GetByNidAsync(nid, ct) is not null)
            throw new NidInUseException("National ID is already in use.");

        string? email = null, passwordHash = null;
        var hasLogin = !string.IsNullOrWhiteSpace(request.Email);
        if (hasLogin)
        {
            email = request.Email!.Trim().ToLowerInvariant();
            if (await _users.GetByEmailAsync(email, ct) is not null) return null;
            passwordHash = _hasher.Hash(request.Password!);
        }

        var user = new User(
            request.FullName, role.Id, nid, email: email, passwordHash: passwordHash,
            phone: request.Phone, gender: request.Gender, age: request.Age,
            mustChangePassword: hasLogin);
        user.AssignCode(await NextCodeAsync(role.Code, ct));

        await _users.AddAsync(user, ct);
        var profile = await AddProfileAsync(user.Id, role.Code, request.MonthlySalary,
            request.DailyWage, request.HireDate, ct);
        await _users.SaveChangesAsync(ct); // one context → user + profile commit atomically

        return Map(user, role.Code, profile);
    }

    #endregion

    #region UpdateAsync — update user, profile, status

    public async Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user is null) return null;

        var role = await _roles.GetByIdAsync(user.RoleId, ct)
            ?? throw new InvalidUserException("User role not found.");
        if (!string.Equals(request.Role, role.Code, StringComparison.Ordinal))
            throw new InvalidUserException("Role cannot be changed.");

        var nid = request.Nid.Trim();
        if (!string.Equals(nid, user.Nid, StringComparison.Ordinal))
        {
            var byNid = await _users.GetByNidAsync(nid, ct);
            if (byNid is not null && byNid.Id != user.Id)
                throw new NidInUseException("National ID is already in use.");
        }

        var email = string.IsNullOrWhiteSpace(request.Email)
            ? null : request.Email.Trim().ToLowerInvariant();
        if (email is not null && !string.Equals(email, user.Email, StringComparison.Ordinal))
        {
            var byEmail = await _users.GetByEmailAsync(email, ct);
            if (byEmail is not null && byEmail.Id != user.Id)
                throw new EmailInUseException("Email is already in use.");
        }

        user.UpdateProfile(request.FullName, email, nid,
            phone: request.Phone, gender: request.Gender, age: request.Age);
        if (request.Status == "active") user.Activate(); else user.Deactivate();
        if (!string.IsNullOrWhiteSpace(request.Password)) user.SetPassword(_hasher.Hash(request.Password));

        var profile = await UpsertProfileAsync(user.Id, role.Code, request.MonthlySalary,
            request.DailyWage, request.HireDate, ct);
        await _users.SaveChangesAsync(ct);

        return Map(user, role.Code, profile);
    }

    #endregion

    #region SetStatusAsync — activate or deactivate

    public async Task<UserDto?> SetStatusAsync(Guid id, SetUserStatusRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user is null) return null;

        if (request.Status == "active") user.Activate(); else user.Deactivate();
        await _users.SaveChangesAsync(ct);

        var role = await _roles.GetByIdAsync(user.RoleId, ct);
        var roleCode = role?.Code ?? string.Empty;
        return Map(user, roleCode, await LoadProfileAsync(user.Id, roleCode, ct));
    }

    #endregion

    #endregion

    #region Helpers

    private async Task<UserProfileDto?> LoadProfileAsync(Guid userId, string roleCode, CancellationToken ct)
    {
        switch (roleCode)
        {
            case "manager":
                var manager = await _profiles.GetManagerAsync(userId, ct);
                return manager is null ? null : ManagerProfile(manager);
            case "moqawel":
                var moqawel = await _profiles.GetMoqawelAsync(userId, ct);
                return moqawel is null ? null : MoqawelProfile(moqawel);
            case "worker":
                var worker = await _profiles.GetWorkerAsync(userId, ct);
                return worker is null ? null : WorkerProfile(worker);
            default:
                return null;
        }
    }

    private async Task<string> NextCodeAsync(string roleCode, CancellationToken ct)
    {
        var prefix = CodePrefixes[roleCode];
        var codes = await _users.ListCodesByPrefixAsync(prefix + "-", ct);
        var next = codes
            .Select(c => int.TryParse(c[(prefix.Length + 1)..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"{prefix}-{next}";
    }

    private async Task<UserProfileDto?> AddProfileAsync(
        Guid userId, string roleCode, decimal? monthlySalary,
        decimal? dailyWage, DateOnly? hireDate, CancellationToken ct)
    {
        switch (roleCode)
        {
            case "manager":
                var manager = new Manager(userId, monthlySalary!.Value);
                await _profiles.AddManagerAsync(manager, ct);
                return ManagerProfile(manager);
            case "moqawel":
                var moqawel = new Moqawel(userId, dailyWage!.Value);
                await _profiles.AddMoqawelAsync(moqawel, ct);
                return MoqawelProfile(moqawel);
            case "worker":
                var worker = new Worker(userId, dailyWage!.Value, hireDate);
                await _profiles.AddWorkerAsync(worker, ct);
                return WorkerProfile(worker);
            default:
                return null;
        }
    }

    private async Task<UserProfileDto?> UpsertProfileAsync(
        Guid userId, string roleCode, decimal? monthlySalary,
        decimal? dailyWage, DateOnly? hireDate, CancellationToken ct)
    {
        switch (roleCode)
        {
            case "manager":
                var manager = await _profiles.GetManagerAsync(userId, ct);
                if (manager is null) return await AddProfileAsync(userId, roleCode, monthlySalary, dailyWage, hireDate, ct);
                manager.SetMonthlySalary(monthlySalary!.Value);
                return ManagerProfile(manager);
            case "moqawel":
                var moqawel = await _profiles.GetMoqawelAsync(userId, ct);
                if (moqawel is null) return await AddProfileAsync(userId, roleCode, monthlySalary, dailyWage, hireDate, ct);
                moqawel.Update(dailyWage!.Value);
                return MoqawelProfile(moqawel);
            case "worker":
                var worker = await _profiles.GetWorkerAsync(userId, ct);
                if (worker is null) return await AddProfileAsync(userId, roleCode, monthlySalary, dailyWage, hireDate, ct);
                worker.Update(dailyWage!.Value, hireDate);
                return WorkerProfile(worker);
            default:
                return null;
        }
    }

    private static UserProfileDto ManagerProfile(Manager m) => new(m.MonthlySalary, null, null);
    private static UserProfileDto MoqawelProfile(Moqawel m) => new(null, m.DailyWage, null);
    private static UserProfileDto WorkerProfile(Worker w) => new(null, w.DailyWage, w.HireDate);

    private static UserDto Map(User u, string roleCode, UserProfileDto? profile) => new(
        u.Id, u.Code, u.FullName, u.Email, u.Phone, u.Gender, u.Age, u.Nid,
        roleCode, u.IsActive ? "active" : "disabled", u.MustChangePassword, profile);

    #endregion
}
