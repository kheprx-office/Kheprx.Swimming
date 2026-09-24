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

/// <summary>Composite swimmer profile (GET /api/swimmers/{id}) — identity + latest vitals (null when no exam).</summary>
public sealed record SwimmerProfileDto(SwimmerIdentityDto Identity, SwimmerVitalsDto? Vitals);

public sealed record SwimmerIdentityDto(
    Guid Id, string Uid, string NameEn, string? NameAr, DateOnly? Dob, int? Age,
    string GenderCode, string? Phone, string? TrainingClubNameEn, string? TrainingClubNameAr);

public sealed record SwimmerVitalsDto(
    Guid Id, DateOnly ExamDate, CodedLookupDto? BloodType, decimal Hemoglobin, decimal HeightCm, decimal WeightKg,
    CodedLookupDto InternalMed, CodedLookupDto HeartAssess, CodedLookupDto SpineAssess);

/// <summary>Payload to update a swimmer's identity (PUT /api/swimmers/{id}/identity).</summary>
public sealed record UpdateSwimmerIdentityRequest(string NameEn, string? NameAr, DateOnly Dob, string? Phone);

/// <summary>Payload to record a new dated medical exam (POST /api/swimmers/{id}/medical-exams).</summary>
public sealed record CreateMedicalExamRequest(
    DateOnly ExamDate, Guid? BloodTypeId, decimal Hemoglobin, decimal HeightCm, decimal WeightKg,
    Guid InternalMedId, Guid HeartAssessId, Guid SpineAssessId);

/// <summary>One stroke's swimmer count for the dashboard stroke split (GET /api/dashboard/summary).</summary>
public sealed record StrokeSplitDto(Guid StrokeId, string Code, string NameEn, string? NameAr, int Count);
