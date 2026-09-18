using Kheprx.BaseBackend.Identity.Application.Abstractions;
using Kheprx.BaseBackend.Identity.Application.DTOs;
using Kheprx.BaseBackend.Identity.Application.Options;
using Kheprx.BaseBackend.Identity.Application.Services.Interfaces;
using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Exceptions;
using Kheprx.BaseBackend.Identity.Domain.ReadModels;
using Kheprx.BaseBackend.Identity.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Kheprx.BaseBackend.Identity.Application.Services;

internal sealed class SwimmerService : ISwimmerService
{
    private readonly ISwimmerProfileRepository _swimmers;
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IClubRepository _clubs;
    private readonly IBloodTypeRepository _bloodTypes;
    private readonly IStrokeRepository _strokes;
    private readonly IGenderRepository _genders;
    private readonly IPasswordHasher _hasher;
    private readonly AccountCreationOptions _options;

    public SwimmerService(
        ISwimmerProfileRepository swimmers, IUserRepository users, IRoleRepository roles,
        IClubRepository clubs, IBloodTypeRepository bloodTypes, IStrokeRepository strokes,
        IGenderRepository genders, IPasswordHasher hasher, IOptions<AccountCreationOptions> options)
    {
        _swimmers = swimmers;
        _users = users;
        _roles = roles;
        _clubs = clubs;
        _bloodTypes = bloodTypes;
        _strokes = strokes;
        _genders = genders;
        _hasher = hasher;
        _options = options.Value;
    }

    public async Task<SwimmerCountDto> GetCountAsync(CancellationToken ct = default)
        => new(await _swimmers.CountAsync(ct));

    public async Task<IReadOnlyList<SwimmerListItemDto>> ListAsync(string? search = null, CancellationToken ct = default)
    {
        var rows = await _swimmers.ListAsync(search, ct);
        return rows.Select(r => new SwimmerListItemDto(
            r.Id, r.Uid, r.NameEn, r.NameAr, r.ClubNameEn, r.ClubNameAr,
            r.GenderCode ?? string.Empty, ComputeAge(r.Dob))).ToList();
    }

    private static int? ComputeAge(DateOnly? dob)
    {
        if (dob is null) return null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - dob.Value.Year;
        if (dob.Value > today.AddYears(-age)) age--;
        return age;
    }

    public async Task<CreatedSwimmerDto?> CreateAsync(CreateSwimmerRequest request, CancellationToken ct = default)
    {
        var role = await _roles.GetByCodeAsync("swimmer", ct)
            ?? throw new InvalidUserException("Swimmer role is not configured.");

        if (await _users.GetByUsernameAsync(request.Username, ct) is not null) return null;

        string? email = null;
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            email = request.Email.Trim().ToLowerInvariant();
            if (await _users.GetByEmailAsync(email, ct) is not null) return null;
        }

        await EnsureReferencesExist(request, ct);

        var uid = $"SW-{await _swimmers.GetMaxUidNumberAsync(ct) + 1:D4}";
        var passwordHash = _hasher.Hash(_options.GenericPassword);

        var user = new AppUser(
            request.Username, request.NameEn, role.Id,
            nameAr: request.NameAr, email: email, passwordHash: passwordHash,
            genderId: request.GenderId, dob: request.Dob, phone: request.Phone, isFirstLogin: true);
        await _users.AddAsync(user, ct);

        var profile = new SwimmerProfile(user.Id, uid, request.TrainingClubId,
            request.RepresentChampionshipClubId, request.BloodTypeId);
        await _swimmers.AddAsync(profile, ct);

        await _swimmers.AddSpecializationsAsync(
            request.StrokeIds.Select(sid => new SwimmerSpecialization(profile.Id, sid)), ct);

        if (!await _swimmers.SaveChangesAsync(ct))
            return null; // unique-index race — surface as a conflict (controller → 409)

        return new CreatedSwimmerDto(profile.Id, uid, user.Username, user.NameEn, _options.GenericPassword);
    }

    private async Task EnsureReferencesExist(CreateSwimmerRequest r, CancellationToken ct)
    {
        if (!await _genders.ExistsAsync(r.GenderId, ct)) throw new InvalidUserException("Unknown gender.");
        if (!await _clubs.ExistsAsync(r.TrainingClubId, ct)) throw new InvalidUserException("Unknown training club.");
        if (r.RepresentChampionshipClubId is { } champ && !await _clubs.ExistsAsync(champ, ct)) throw new InvalidUserException("Unknown championship club.");
        if (!await _bloodTypes.ExistsAsync(r.BloodTypeId, ct)) throw new InvalidUserException("Unknown blood type.");
        foreach (var sid in r.StrokeIds)
            if (!await _strokes.ExistsAsync(sid, ct)) throw new InvalidUserException("Unknown stroke.");
    }
}
