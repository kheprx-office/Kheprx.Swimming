namespace Kheprx.BaseBackend.Championships.Domain.Repositories;

// Read shape returned by GetByEventAsync (carries persisted ids).
public sealed record RaceResultRow(Guid Id, Guid RaceSessionId, Guid SwimmerId, int TimeMs, int Points, bool IsPersonalBest, Guid RecordedBy);

// Write shape accepted by ReplaceForSessionAsync (ids generated on insert).
public sealed record RaceResultInput(Guid SwimmerId, int TimeMs, int Points, bool IsPersonalBest, Guid RecordedBy);

// Flat join row for a swimmer's results across events (race_result -> race_session -> competition_day).
public sealed record SwimmerRaceLineRow(
    Guid EventId, string DayLabelEn, string? DayLabelAr, DateOnly DayDate,
    TimeOnly? ScheduledTime, Guid DistanceId, Guid StrokeId, int TimeMs, bool IsPersonalBest);
