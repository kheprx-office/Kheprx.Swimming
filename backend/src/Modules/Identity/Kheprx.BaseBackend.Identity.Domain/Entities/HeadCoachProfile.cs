namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class HeadCoachProfile
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string NationalId { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private HeadCoachProfile() { } // EF Core

    public HeadCoachProfile(Guid userId, string nationalId)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        NationalId = nationalId.Trim();
        CreatedAt = DateTime.UtcNow;
    }
}
