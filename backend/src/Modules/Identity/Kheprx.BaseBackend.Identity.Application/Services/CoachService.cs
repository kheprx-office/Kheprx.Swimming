using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Options;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Exceptions;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Kheprx.BaseBackend.Identity.Application.Services;

internal sealed class CoachService : ICoachService
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly ICoachProfileRepository _coaches;
    private readonly IGenderRepository _genders;
    private readonly IPasswordHasher _hasher;
    private readonly AccountCreationOptions _options;

    public CoachService(
        IUserRepository users, IRoleRepository roles, ICoachProfileRepository coaches,
        IGenderRepository genders, IPasswordHasher hasher, IOptions<AccountCreationOptions> options)
    {
        _users = users;
        _roles = roles;
        _coaches = coaches;
        _genders = genders;
        _hasher = hasher;
        _options = options.Value;
    }

    public async Task<CreatedCoachDto?> CreateAsync(CreateCoachRequest request, CancellationToken ct = default)
    {
        var role = await _roles.GetByCodeAsync(request.Role, ct)
            ?? throw new InvalidUserException($"Unknown role '{request.Role}'.");

        if (await _users.GetByUsernameAsync(request.Username, ct) is not null) return null;

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _users.GetByEmailAsync(email, ct) is not null) return null;

        var nationalId = request.NationalId.Trim();
        if (await _coaches.NationalIdExistsAsync(nationalId, ct)) return null;

        if (!await _genders.ExistsAsync(request.GenderId, ct)) throw new InvalidUserException("Unknown gender.");

        var passwordHash = _hasher.Hash(_options.GenericPassword);
        var user = new AppUser(
            request.Username, request.NameEn, role.Id,
            nameAr: request.NameAr, email: email, passwordHash: passwordHash,
            genderId: request.GenderId, dob: request.Dob, phone: request.Phone, isFirstLogin: true);
        await _users.AddAsync(user, ct);

        if (role.Code == "captain")
            await _coaches.AddCaptainAsync(new CaptainProfile(user.Id, nationalId), ct);
        else
            await _coaches.AddHeadCoachAsync(new HeadCoachProfile(user.Id, nationalId), ct);

        if (!await _coaches.SaveChangesAsync(ct))
            return null; // unique-index race — surface as a conflict (controller → 409)

        return new CreatedCoachDto(user.Id, user.Username, user.NameEn, role.Code, _options.GenericPassword);
    }
}
