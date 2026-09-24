namespace Kheprx.BaseBackend.Championships.Application.DTOs;

// ---- Read (GET /schedule) ----
public sealed record ScheduleRaceDto(Guid Id, Guid StrokeId, Guid DistanceId, TimeOnly? ScheduledTime, IReadOnlyList<Guid> SwimmerIds);
public sealed record ScheduleDayDto(Guid Id, string LabelEn, string? LabelAr, DateOnly DayDate, IReadOnlyList<ScheduleRaceDto> Races);
public sealed record ScheduleDto(IReadOnlyList<ScheduleDayDto> Days);

// ---- Write (PUT /schedule) ----
public sealed record SetScheduleRace(Guid StrokeId, Guid DistanceId, TimeOnly? ScheduledTime, IReadOnlyList<Guid> SwimmerIds);
public sealed record SetScheduleDay(string LabelEn, string? LabelAr, DateOnly DayDate, IReadOnlyList<SetScheduleRace> Races);
public sealed record SetScheduleRequest(IReadOnlyList<SetScheduleDay> Days);

public enum SetScheduleOutcome { Ok, NotFound, Invalid }

/// <summary>Result of a schedule replace: Ok carries the re-read tree; Invalid carries a short reason code.</summary>
public sealed record SetScheduleResult(SetScheduleOutcome Outcome, ScheduleDto? Saved, string? Error);
