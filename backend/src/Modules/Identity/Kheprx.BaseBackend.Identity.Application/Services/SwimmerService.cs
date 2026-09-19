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
    private readonly IStrokeRepository _strokes;
    private readonly IGenderRepository _genders;
    private readonly IFitnessAssessmentRepository _fitness;
    private readonly IBloodTypeRepository _bloodTypes;
    private readonly IPasswordHasher _hasher;
    private readonly AccountCreationOptions _options;

    public SwimmerService(
        ISwimmerProfileRepository swimmers, IUserRepository users, IRoleRepository roles,
        IClubRepository clubs, IStrokeRepository strokes, IGenderRepository genders,
        IFitnessAssessmentRepository fitness, IBloodTypeRepository bloodTypes,
        IPasswordHasher hasher, IOptions<AccountCreationOptions> options)
    {
        _swimmers = swimmers;
        _users = users;
        _roles = roles;
        _clubs = clubs;
        _strokes = strokes;
        _genders = genders;
        _fitness = fitness;
        _bloodTypes = bloodTypes;
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
            request.RepresentChampionshipClubId);
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
        foreach (var sid in r.StrokeIds)
            if (!await _strokes.ExistsAsync(sid, ct)) throw new InvalidUserException("Unknown stroke.");
    }

    public async Task<SwimmerProfileDto?> GetProfileAsync(Guid id, CancellationToken ct = default)
    {
        var row = await _swimmers.GetProfileByIdAsync(id, ct);
        if (row is null) return null;

        var identity = new SwimmerIdentityDto(
            row.Id, row.Uid, row.NameEn, row.NameAr, row.Dob, ComputeAge(row.Dob),
            row.GenderCode ?? string.Empty, row.Phone, row.TrainingClubNameEn, row.TrainingClubNameAr);

        var exam = await _swimmers.GetLatestExamAsync(id, ct);
        return new SwimmerProfileDto(identity, MapVitals(exam));
    }

    private static SwimmerVitalsDto? MapVitals(Kheprx.BaseBackend.Identity.Domain.ReadModels.MedicalExamRow? e)
    {
        if (e is null) return null;
        var blood = e.BloodTypeId is null
            ? null
            : new CodedLookupDto(e.BloodTypeId.Value, e.BloodTypeCode!, e.BloodTypeNameEn!, e.BloodTypeNameAr);
        return new SwimmerVitalsDto(
            e.Id, e.ExamDate, blood, e.Hemoglobin, e.HeightCm, e.WeightKg,
            new CodedLookupDto(e.InternalMedId, e.InternalMedCode, e.InternalMedNameEn, e.InternalMedNameAr),
            new CodedLookupDto(e.HeartAssessId, e.HeartAssessCode, e.HeartAssessNameEn, e.HeartAssessNameAr),
            new CodedLookupDto(e.SpineAssessId, e.SpineAssessCode, e.SpineAssessNameEn, e.SpineAssessNameAr));
    }

    public async Task<bool> UpdateIdentityAsync(Guid id, UpdateSwimmerIdentityRequest request, CancellationToken ct = default)
    {
        var profile = await _swimmers.GetByIdTrackedAsync(id, ct);
        if (profile is null) return false;

        var user = await _users.GetByIdAsync(profile.UserId, ct);
        if (user is null) return false;

        user.UpdateProfile(request.NameEn, request.NameAr, user.Email, user.GenderId, request.Dob, request.Phone);
        await _users.SaveChangesAsync(ct);
        return true;
    }

    public async Task<SwimmerVitalsDto?> CreateExamAsync(Guid id, CreateMedicalExamRequest request, CancellationToken ct = default)
    {
        var profile = await _swimmers.GetByIdTrackedAsync(id, ct);
        if (profile is null) return null;

        await EnsureExamReferencesExist(request, ct);

        var exam = new MedicalExam(id, request.ExamDate, request.InternalMedId, request.HeartAssessId,
            request.SpineAssessId, request.BloodTypeId, request.Hemoglobin, request.HeightCm, request.WeightKg);
        await _swimmers.AddExamAsync(exam, ct);
        await _swimmers.SaveChangesAsync(ct);

        return MapVitals(await _swimmers.GetExamRowByIdAsync(exam.Id, ct));
    }

    private async Task EnsureExamReferencesExist(CreateMedicalExamRequest r, CancellationToken ct)
    {
        if (!await _fitness.ExistsAsync(r.InternalMedId, ct)) throw new InvalidUserException("Unknown internal medicine assessment.");
        if (!await _fitness.ExistsAsync(r.HeartAssessId, ct)) throw new InvalidUserException("Unknown heart assessment.");
        if (!await _fitness.ExistsAsync(r.SpineAssessId, ct)) throw new InvalidUserException("Unknown spine assessment.");
        if (r.BloodTypeId is { } bt && !await _bloodTypes.ExistsAsync(bt, ct)) throw new InvalidUserException("Unknown blood type.");
    }

    public async Task<IReadOnlyList<SwimmerVitalsDto>?> ListExamsAsync(Guid swimmerId, CancellationToken ct = default)
    {
        var profile = await _swimmers.GetByIdTrackedAsync(swimmerId, ct);
        if (profile is null) return null;
        var rows = await _swimmers.ListExamsAsync(swimmerId, ct);
        return rows.Select(r => MapVitals(r)!).ToList();
    }

    public async Task<SwimmerVitalsDto?> UpdateExamAsync(Guid swimmerId, Guid examId, CreateMedicalExamRequest request, CancellationToken ct = default)
    {
        var exam = await _swimmers.GetExamTrackedAsync(examId, ct);
        if (exam is null || exam.SwimmerId != swimmerId) return null;

        await EnsureExamReferencesExist(request, ct);

        exam.Update(request.ExamDate, request.InternalMedId, request.HeartAssessId, request.SpineAssessId,
            request.BloodTypeId, request.Hemoglobin, request.HeightCm, request.WeightKg);
        await _swimmers.SaveChangesAsync(ct);

        return MapVitals(await _swimmers.GetExamRowByIdAsync(examId, ct));
    }

    public async Task<bool> DeleteExamAsync(Guid swimmerId, Guid examId, CancellationToken ct = default)
    {
        var exam = await _swimmers.GetExamTrackedAsync(examId, ct);
        if (exam is null || exam.SwimmerId != swimmerId) return false;

        _swimmers.RemoveExam(exam);
        await _swimmers.SaveChangesAsync(ct);
        return true;
    }

    public async Task<SwimmerGuardiansDto?> GetGuardiansAsync(Guid id, CancellationToken ct = default)
    {
        var profile = await _swimmers.GetByIdTrackedAsync(id, ct);
        if (profile is null) return null;

        var rows = await _swimmers.ListGuardiansAsync(id, ct);
        return new SwimmerGuardiansDto(
            MapGuardian(rows.FirstOrDefault(r => r.RelationCode == "father")),
            MapGuardian(rows.FirstOrDefault(r => r.RelationCode == "mother")));
    }

    private static GuardianDto? MapGuardian(Kheprx.BaseBackend.Identity.Domain.ReadModels.GuardianRow? r)
        => r is null ? null : new GuardianDto(r.Id, r.RelationCode, r.Name, r.NationalId, r.Phone);

    public async Task<bool> UpsertGuardiansAsync(Guid id, UpsertGuardiansRequest request, CancellationToken ct = default)
    {
        var profile = await _swimmers.GetByIdTrackedAsync(id, ct);
        if (profile is null) return false;

        await UpsertOne(id, "father", request.Father, ct);
        await UpsertOne(id, "mother", request.Mother, ct);
        await _swimmers.SaveChangesAsync(ct);
        return true;
    }

    private async Task UpsertOne(Guid swimmerId, string relationCode, GuardianInputDto input, CancellationToken ct)
    {
        var relationId = await _swimmers.GetGuardianRelationIdByCodeAsync(relationCode, ct)
            ?? throw new InvalidUserException($"Guardian relation '{relationCode}' is not configured.");

        var existing = await _swimmers.GetGuardianTrackedAsync(swimmerId, relationId, ct);
        if (existing is null)
            await _swimmers.AddGuardianAsync(new Guardian(swimmerId, relationId, input.Name, input.NationalId, input.Phone), ct);
        else
            existing.Update(input.Name, input.NationalId, input.Phone);
    }
}
