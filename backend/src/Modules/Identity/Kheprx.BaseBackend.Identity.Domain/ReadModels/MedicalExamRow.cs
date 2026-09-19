namespace Kheprx.BaseBackend.Identity.Domain.ReadModels;

/// <summary>Flattened latest-exam row — vitals joined to blood_type + fitness_assessment (×3).</summary>
public sealed record MedicalExamRow(
    Guid Id,
    DateOnly ExamDate,
    decimal Hemoglobin,
    decimal HeightCm,
    decimal WeightKg,
    Guid? BloodTypeId,
    string? BloodTypeCode,
    string? BloodTypeNameEn,
    string? BloodTypeNameAr,
    Guid InternalMedId,
    string InternalMedCode,
    string InternalMedNameEn,
    string? InternalMedNameAr,
    Guid HeartAssessId,
    string HeartAssessCode,
    string HeartAssessNameEn,
    string? HeartAssessNameAr,
    Guid SpineAssessId,
    string SpineAssessCode,
    string SpineAssessNameEn,
    string? SpineAssessNameAr);
