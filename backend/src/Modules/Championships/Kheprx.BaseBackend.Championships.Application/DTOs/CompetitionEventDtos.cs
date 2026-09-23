namespace Kheprx.BaseBackend.Championships.Application.DTOs;

/// <summary>A championship event. Status* are resolved in the API layer (empty from the service).</summary>
public sealed record CompetitionEventDto(
    Guid Id,
    string NameEn,
    string? NameAr,
    DateOnly StartDate,
    DateOnly EndDate,
    string LocationEn,
    string? LocationAr,
    Guid StatusId,
    string StatusCode,
    string StatusNameEn,
    string? StatusNameAr);

/// <summary>Create-championship request body. The single Name/Location are mapped to the
/// current-language column in the API layer (English is the required anchor column).</summary>
public sealed record CreateCompetitionEventRequest(string Name, DateOnly StartDate, DateOnly EndDate, string Location);

/// <summary>Resolved create command handed to the service — language mapping, status id and creator
/// are already applied by the API layer.</summary>
public sealed record CreateCompetitionEventCommand(
    string NameEn,
    string? NameAr,
    DateOnly StartDate,
    DateOnly EndDate,
    string LocationEn,
    string? LocationAr,
    Guid StatusId,
    Guid CreatedBy);
