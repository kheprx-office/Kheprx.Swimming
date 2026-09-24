namespace Kheprx.BaseBackend.Championships.Domain.Entities;

public sealed class RaceAssignment
{
    public Guid Id { get; private set; }
    public Guid RaceSessionId { get; private set; }
    public Guid SwimmerId { get; private set; }

    private RaceAssignment() { } // EF Core

    public RaceAssignment(Guid raceSessionId, Guid swimmerId)
    {
        Id = Guid.NewGuid();
        RaceSessionId = raceSessionId;
        SwimmerId = swimmerId;
    }
}
