using Kheprx.BaseBackend.Identity.Domain.Entities;
using Kheprx.BaseBackend.Identity.Domain.Exceptions;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class SwimmerProfileTests
{
    [Fact]
    public void Ctor_assigns_id_uid_fks_and_timestamps()
    {
        var userId = Guid.NewGuid();
        var clubId = Guid.NewGuid();

        var p = new SwimmerProfile(userId, " SW-0007 ", clubId);

        Assert.NotEqual(Guid.Empty, p.Id);
        Assert.Equal(userId, p.UserId);
        Assert.Equal("SW-0007", p.Uid);
        Assert.Equal(clubId, p.TrainingClubId);
        Assert.Null(p.RepresentChampionshipClubId);
        Assert.NotEqual(default, p.CreatedAt);
        Assert.Equal(p.CreatedAt, p.UpdatedAt);
    }

    [Fact]
    public void Specialization_ctor_sets_composite_members()
    {
        var swimmerId = Guid.NewGuid();
        var strokeId = Guid.NewGuid();

        var s = new SwimmerSpecialization(swimmerId, strokeId);

        Assert.Equal(swimmerId, s.SwimmerProfileId);
        Assert.Equal(strokeId, s.StrokeId);
    }

    [Fact]
    public void SetTrainingClub_updates_id()
    {
        var p = new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid());
        var newClub = Guid.NewGuid();
        p.SetTrainingClub(newClub);
        Assert.Equal(newClub, p.TrainingClubId);
    }

    [Fact]
    public void SetTrainingClub_rejects_empty()
    {
        var p = new SwimmerProfile(Guid.NewGuid(), "SW-0001", Guid.NewGuid());
        Assert.Throws<InvalidUserException>(() => p.SetTrainingClub(Guid.Empty));
    }
}
