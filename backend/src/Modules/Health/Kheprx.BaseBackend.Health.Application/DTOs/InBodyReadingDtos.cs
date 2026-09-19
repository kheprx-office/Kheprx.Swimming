namespace Kheprx.BaseBackend.Health.Application.DTOs;

/// <summary>Payload to create/update an InBody reading (date user-provided; recorded_by from the current user).</summary>
public sealed record CreateInBodyReadingRequest(
    DateOnly ReadingDate,
    decimal HeightCm,
    decimal WeightKg,
    decimal FatPct,
    decimal MusclePct,
    decimal BoneDensity,
    decimal BodyDensity);

/// <summary>An InBody reading.</summary>
public sealed record InBodyReadingDto(
    Guid Id,
    DateOnly ReadingDate,
    decimal HeightCm,
    decimal WeightKg,
    decimal FatPct,
    decimal MusclePct,
    decimal BoneDensity,
    decimal BodyDensity,
    Guid RecordedBy);
