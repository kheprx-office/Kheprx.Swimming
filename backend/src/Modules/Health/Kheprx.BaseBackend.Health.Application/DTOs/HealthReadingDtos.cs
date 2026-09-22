namespace Kheprx.BaseBackend.Health.Application.DTOs;

/// <summary>Payload to log a health reading (POST /api/health-readings).</summary>
public sealed record CreateHealthReadingRequest(
    Guid SwimmerId,
    Guid MedicalTestId,
    decimal Value);

/// <summary>A logged health reading. Status is derived vs the test bounds, not persisted.</summary>
public sealed record HealthReadingDto(
    Guid Id,
    Guid SwimmerId,
    Guid MedicalTestId,
    decimal Value,
    DateTime ReadingDate,
    Guid RecordedBy,
    string Status);

/// <summary>Payload to edit a health reading (PUT /api/health-readings/{id}). Value only.</summary>
public sealed record UpdateHealthReadingRequest(decimal Value);

/// <summary>An enriched health reading row (test name/unit/bounds + derived status).</summary>
public sealed record HealthReadingListItemDto(
    Guid Id,
    Guid MedicalTestId,
    string TestNameEn,
    string TestNameAr,
    string Unit,
    decimal Value,
    decimal LowerBound,
    decimal UpperBound,
    DateTime ReadingDate,
    string Status);
