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

    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IPasswordHasher _hasher;

    #endregion

    #region Constructor

    public UserService(IUserRepository users, IRoleRepository roles, IPasswordHasher hasher)
    {
        _users = users;
        _roles = roles;
        _hasher = hasher;
    }

    #endregion

    #region APIs

    #region ListAsync — list users with role code, optional search

    public async Task<IReadOnlyList<UserDto>> ListAsync(string? search = null, CancellationToken ct = default)
    {
        var users = await _users.ListAsync(search, ct);
        var roles = await _roles.GetAllAsync(ct);
        var codeById = roles.ToDictionary(r => r.Id, r => r.Code);

        return users.Select(u =>
        {
            var roleCode = codeById.TryGetValue(u.RoleId, out var code) ? code : string.Empty;
            return Map(u, roleCode);
        }).ToList();
    }

    #endregion

    #region CreateAsync — create user with optional email/password

    public async Task<UserDto?> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var role = await _roles.GetByCodeAsync(request.Role, ct)
            ?? throw new InvalidUserException($"Unknown role '{request.Role}'.");

        string? email = null, passwordHash = null;
        var hasLogin = !string.IsNullOrWhiteSpace(request.Email);
        if (hasLogin)
        {
            email = request.Email!.Trim().ToLowerInvariant();
            if (await _users.GetByEmailAsync(email, ct) is not null) return null;
            passwordHash = _hasher.Hash(request.Password!);
        }

        var user = new AppUser(
            request.Username, request.NameEn, role.Id,
            nameAr: request.NameAr, email: email, passwordHash: passwordHash,
            genderId: request.GenderId, dob: request.Dob, phone: request.Phone,
            isFirstLogin: hasLogin);

        await _users.AddAsync(user, ct);
        await _users.SaveChangesAsync(ct);

        return Map(user, role.Code);
    }

    #endregion

    #region UpdateAsync — update user profile, optional password

    public async Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user is null) return null;

        var role = await _roles.GetByIdAsync(user.RoleId, ct)
            ?? throw new InvalidUserException("User role not found.");

        if (!string.Equals(request.Role, role.Code, StringComparison.Ordinal))
            throw new InvalidUserException("Role cannot be changed.");

        var email = string.IsNullOrWhiteSpace(request.Email)
            ? null : request.Email.Trim().ToLowerInvariant();
        if (email is not null && !string.Equals(email, user.Email, StringComparison.Ordinal))
        {
            var byEmail = await _users.GetByEmailAsync(email, ct);
            if (byEmail is not null && byEmail.Id != user.Id)
                throw new EmailInUseException("Email is already in use.");
        }

        user.UpdateProfile(request.NameEn, request.NameAr, email, request.GenderId, request.Dob, request.Phone);
        if (!string.IsNullOrWhiteSpace(request.Password)) user.SetPassword(_hasher.Hash(request.Password));

        await _users.SaveChangesAsync(ct);

        return Map(user, role.Code);
    }

    #endregion

    #endregion

    #region Helpers

    private static UserDto Map(AppUser u, string roleCode) => new(
        u.Id, u.Username, u.NameEn, u.NameAr, u.Email,
        u.Phone, u.GenderId, u.Dob, roleCode);

    #endregion
}
