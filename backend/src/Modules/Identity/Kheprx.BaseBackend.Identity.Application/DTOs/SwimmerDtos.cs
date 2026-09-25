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

/// <summary>Prefill for the swimmer first-login wizard, Step 1 (GET /api/swimmers/me/onboarding/identity-vitals).</summary>
public sealed record OnboardingPrefillDto(
    string Uid, string NameEn, string? NameAr, Guid? GenderId, DateOnly? Dob, Guid? TrainingClubId, string? Phone);

/// <summary>Step 1 submission (POST /api/swimmers/me/onboarding/identity-vitals): identity edits + a new medical exam.</summary>
public sealed record CompleteIdentityVitalsRequest(
    string NameEn, string? NameAr, Guid GenderId, DateOnly Dob, Guid TrainingClubId,
    DateOnly ExamDate, Guid? BloodTypeId, decimal Hemoglobin, decimal HeightCm, decimal WeightKg,
    Guid InternalMedId, Guid HeartAssessId, Guid SpineAssessId, string? Phone);

/// <summary>Result of completing an onboarding step: the refreshed first-login flag (false once done).</summary>
public sealed record OnboardingStepResultDto(bool MustChangePassword);

/// <summary>One medical-history observation captured in onboarding Step 2 (a "Yes" answer with details).</summary>
public sealed record OnboardingMedicalItemDto(Guid CategoryId, string FieldLabel, string Value);

/// <summary>Step 2 submission (POST /api/swimmers/me/onboarding/guardian-medical): both guardians + Yes-only medical items.</summary>
public sealed record CompleteGuardianMedicalRequest(
    GuardianInputDto Father,
    GuardianInputDto Mother,
    IReadOnlyList<OnboardingMedicalItemDto> Medical);

/// <summary>The caller's own swimmer profile id (GET /api/swimmers/me).</summary>
public sealed record MySwimmerRefDto(Guid SwimmerId);
