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
