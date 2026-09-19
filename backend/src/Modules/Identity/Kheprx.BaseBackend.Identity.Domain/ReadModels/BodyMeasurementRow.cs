namespace Kheprx.BaseBackend.Identity.Domain.ReadModels;

/// <summary>Flattened latest body-measurement row.</summary>
public sealed record BodyMeasurementRow(
    Guid Id,
    DateOnly MeasuredAt,
    decimal RightArmCm,
    decimal LeftArmCm,
    decimal RightLegCm,
    decimal LeftLegCm,
    decimal TorsoCm,
    decimal BustDiameterCm,
    decimal WaistDiameterCm);
