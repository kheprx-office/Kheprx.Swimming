namespace Kheprx.BaseBackend.Championships.Domain.Entities;

public sealed class RaceSession
{
    public Guid Id { get; private set; }
    public Guid DayId { get; private set; }
    public Guid StrokeId { get; private set; }
    public Guid DistanceId { get; private set; }
    public TimeOnly? ScheduledTime { get; private set; }

    private RaceSession() { } // EF Core

    public RaceSession(Guid dayId, Guid strokeId, Guid distanceId, TimeOnly? scheduledTime)
    {
        Id = Guid.NewGuid();
        DayId = dayId;
        StrokeId = strokeId;
        DistanceId = distanceId;
        ScheduledTime = scheduledTime;
    }
}
