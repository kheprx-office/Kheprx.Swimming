namespace Kheprx.BaseBackend.Championships.Application.DTOs;

// ---- Read (GET /results) ----
public sealed record RaceResultDto(Guid RaceSessionId, Guid SwimmerId, int TimeMs, int Points, bool IsPersonalBest);
public sealed record ResultsDto(IReadOnlyList<RaceResultDto> Results);

// ---- Write (PUT /races/{id}/results) ----
public sealed record SetRaceResultsEntry(Guid SwimmerId, int TimeMs);
public sealed record SetRaceResultsRequest(IReadOnlyList<SetRaceResultsEntry> Entries);

public enum SetRaceResultsOutcome { Ok, NotFound, Invalid }

/// <summary>Result of a per-race results replace: Ok carries the event's re-read results; Invalid carries a reason code.</summary>
public sealed record SetRaceResultsResult(SetRaceResultsOutcome Outcome, ResultsDto? Saved, string? Error);
