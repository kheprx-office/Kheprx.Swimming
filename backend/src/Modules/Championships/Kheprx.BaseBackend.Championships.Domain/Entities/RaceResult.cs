namespace Kheprx.BaseBackend.Championships.Domain.Entities;

public sealed class RaceResult
{
    public Guid Id { get; private set; }
    public Guid RaceSessionId { get; private set; }
    public Guid SwimmerId { get; private set; }
    public int TimeMs { get; private set; }
    public int Points { get; private set; }
    public bool IsPersonalBest { get; private set; }
    public Guid RecordedBy { get; private set; }

    private RaceResult() { } // EF Core

    public RaceResult(Guid raceSessionId, Guid swimmerId, int timeMs, int points, bool isPersonalBest, Guid recordedBy)
    {
        Id = Guid.NewGuid();
        RaceSessionId = raceSessionId;
        SwimmerId = swimmerId;
        TimeMs = timeMs;
        Points = points;
        IsPersonalBest = isPersonalBest;
        RecordedBy = recordedBy;
    }
}
