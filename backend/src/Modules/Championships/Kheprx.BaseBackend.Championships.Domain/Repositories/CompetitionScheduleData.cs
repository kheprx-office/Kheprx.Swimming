namespace Kheprx.BaseBackend.Championships.Domain.Repositories;

// Read shape returned by GetAsync (carries persisted ids).
public sealed record ScheduleRaceRow(Guid Id, Guid StrokeId, Guid DistanceId, TimeOnly? ScheduledTime, IReadOnlyList<Guid> SwimmerIds);
public sealed record ScheduleDayRow(Guid Id, string LabelEn, string? LabelAr, DateOnly DayDate, IReadOnlyList<ScheduleRaceRow> Races);

// Write shape accepted by ReplaceAsync (ids are generated on insert).
public sealed record ScheduleRaceInput(Guid StrokeId, Guid DistanceId, TimeOnly? ScheduledTime, IReadOnlyList<Guid> SwimmerIds);
public sealed record ScheduleDayInput(string LabelEn, string? LabelAr, DateOnly DayDate, IReadOnlyList<ScheduleRaceInput> Races);
