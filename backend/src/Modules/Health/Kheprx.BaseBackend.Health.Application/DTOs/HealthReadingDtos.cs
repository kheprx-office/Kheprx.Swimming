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
