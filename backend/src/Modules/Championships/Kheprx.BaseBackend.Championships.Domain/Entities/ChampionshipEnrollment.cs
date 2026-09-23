namespace Kheprx.BaseBackend.Championships.Domain.Entities;

public sealed class ChampionshipEnrollment
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid SwimmerId { get; private set; }

    private ChampionshipEnrollment() { } // EF Core

    public ChampionshipEnrollment(Guid eventId, Guid swimmerId)
    {
        Id = Guid.NewGuid();
        EventId = eventId;
        SwimmerId = swimmerId;
    }
}
