namespace Kheprx.BaseBackend.Health.Application.DTOs;

/// <summary>Payload to create a medical test (POST /api/medical-tests).</summary>
public sealed record CreateMedicalTestRequest(
    string NameEn,
    string NameAr,
    string Unit,
    decimal LowerBound,
    decimal UpperBound);

/// <summary>A medical test catalog entry.</summary>
public sealed record MedicalTestDto(
    Guid Id,
    string NameEn,
    string NameAr,
    string Unit,
    decimal LowerBound,
    decimal UpperBound,
    DateTime CreatedAt);
