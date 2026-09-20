namespace Kheprx.BaseBackend.Health.Application.DTOs;

/// <summary>Payload to add a swimmer data field (POST /api/observations).</summary>
public sealed record CreateObservationRequest(
    Guid SwimmerId,
    Guid CategoryId,
    string FieldLabel,
    string Value);

/// <summary>A stored observation (swimmer data field).</summary>
public sealed record ObservationDto(
    Guid Id,
    Guid SwimmerId,
    Guid CategoryId,
    string FieldLabel,
    string Value,
    DateTime ObservedDate,
    Guid RecordedBy);

/// <summary>Payload to edit a swimmer data field (PUT /api/observations/{id}).</summary>
public sealed record UpdateObservationRequest(
    Guid CategoryId,
    string FieldLabel,
    string Value);
