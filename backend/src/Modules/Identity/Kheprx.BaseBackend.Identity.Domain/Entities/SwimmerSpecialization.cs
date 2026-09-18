namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class SwimmerSpecialization
{
    public Guid SwimmerProfileId { get; private set; }
    public Guid StrokeId { get; private set; }

    private SwimmerSpecialization() { } // EF Core

    public SwimmerSpecialization(Guid swimmerProfileId, Guid strokeId)
    {
        SwimmerProfileId = swimmerProfileId;
        StrokeId = strokeId;
    }
}
