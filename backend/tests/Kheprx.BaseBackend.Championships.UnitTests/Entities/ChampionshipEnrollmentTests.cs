using Kheprx.BaseBackend.Championships.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Entities;

public class ChampionshipEnrollmentTests
{
    [Fact]
    public void Ctor_sets_ids_and_generates_a_new_id()
    {
        var eventId = Guid.NewGuid();
        var swimmerId = Guid.NewGuid();

        var e = new ChampionshipEnrollment(eventId, swimmerId);

        Assert.NotEqual(Guid.Empty, e.Id);
        Assert.Equal(eventId, e.EventId);
        Assert.Equal(swimmerId, e.SwimmerId);
    }
}
