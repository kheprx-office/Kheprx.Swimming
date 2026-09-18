namespace Kheprx.BaseBackend.Identity.Application.DTOs;

/// <summary>Total number of tracked swimmers (GET /api/swimmers/count).</summary>
/// <param name="Count">How many swimmer profiles exist.</param>
public sealed record SwimmerCountDto(int Count);

/// <summary>Payload to register a swimmer (POST /api/swimmers).</summary>
public sealed record CreateSwimmerRequest(
    string NameEn,
    string Username,
    Guid TrainingClubId,
    Guid GenderId,
    DateOnly Dob,
    Guid BloodTypeId,
    IReadOnlyList<Guid> StrokeIds,
    string? NameAr,
    string? Email,
    string? Phone,
    Guid? RepresentChampionshipClubId);

/// <summary>Result of a successful swimmer registration.</summary>
public sealed record CreatedSwimmerDto(Guid Id, string Uid, string Username, string NameEn, string TemporaryPassword);

/// <summary>One swimmer row for the roster list (GET /api/swimmers).</summary>
public sealed record SwimmerListItemDto(
    Guid Id,
    string Uid,
    string NameEn,
    string? NameAr,
    string? ClubNameEn,
    string? ClubNameAr,
    string GenderCode,
    int? Age);
