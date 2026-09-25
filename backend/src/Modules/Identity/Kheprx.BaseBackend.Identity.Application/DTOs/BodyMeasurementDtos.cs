namespace Kheprx.BaseBackend.Identity.Application.DTOs;

/// <summary>One body-measurement row for the read shape.</summary>
public sealed record BodyMeasurementDto(
    Guid Id,
    DateOnly MeasuredAt,
    decimal RightArmCm,
    decimal LeftArmCm,
    decimal RightLegCm,
    decimal LeftLegCm,
    decimal TorsoCm,
    decimal BustDiameterCm,
    decimal WaistDiameterCm);

/// <summary>A swimmer's latest body measurement (null when none recorded yet).</summary>
public sealed record SwimmerBodyMeasurementDto(BodyMeasurementDto? Latest);

/// <summary>Payload to record a new dated body measurement (POST). Date is server-set.</summary>
public sealed record CreateBodyMeasurementRequest(
    decimal RightArmCm,
    decimal LeftArmCm,
    decimal RightLegCm,
    decimal LeftLegCm,
    decimal TorsoCm,
    decimal BustDiameterCm,
    decimal WaistDiameterCm);

/// <summary>Step 3 onboarding submission (POST /api/swimmers/me/onboarding/physiological): seven body-measurement values, stored 1:1 (matches the profile Body Measurements tab).</summary>
public sealed record CompletePhysiologicalRequest(
    decimal RightArmCm,
    decimal LeftArmCm,
    decimal RightLegCm,
    decimal LeftLegCm,
    decimal TorsoCm,
    decimal BustDiameterCm,
    decimal WaistDiameterCm);
