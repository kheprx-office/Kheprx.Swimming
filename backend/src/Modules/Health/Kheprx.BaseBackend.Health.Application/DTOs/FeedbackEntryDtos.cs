namespace Kheprx.BaseBackend.Health.Application.DTOs;

/// <summary>Payload to create/update a feedback entry (author + date set server-side).</summary>
public sealed record CreateFeedbackEntryRequest(short Rating, Guid CategoryId, string Comment);

/// <summary>A coach feedback entry. AuthorName* are resolved in the API layer (empty from the service).</summary>
public sealed record FeedbackEntryDto(
    Guid Id,
    Guid SwimmerId,
    short Rating,
    Guid CategoryId,
    string Comment,
    Guid AuthorId,
    string AuthorNameEn,
    string? AuthorNameAr,
    DateOnly EntryDate);
