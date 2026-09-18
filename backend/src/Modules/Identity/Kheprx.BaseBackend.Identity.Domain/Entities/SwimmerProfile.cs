namespace Kheprx.BaseBackend.Identity.Domain.Entities;

public sealed class SwimmerProfile
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Uid { get; private set; } = string.Empty;
    public Guid TrainingClubId { get; private set; }
    public Guid? RepresentChampionshipClubId { get; private set; }
    public Guid? BloodTypeId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private SwimmerProfile() { } // EF Core

    public SwimmerProfile(Guid userId, string uid, Guid trainingClubId,
        Guid? representChampionshipClubId = null, Guid? bloodTypeId = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Uid = uid.Trim();
        TrainingClubId = trainingClubId;
        RepresentChampionshipClubId = representChampionshipClubId;
        BloodTypeId = bloodTypeId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }
}
