using Kheprx.BaseBackend.Identity.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class GuardianTests
{
    [Fact]
    public void Ctor_sets_fields_and_new_id()
    {
        var swimmerId = Guid.NewGuid();
        var relationId = Guid.NewGuid();

        var g = new Guardian(swimmerId, relationId, " Hassan Ali ", "27001010123456", "+201009876543");

        Assert.NotEqual(Guid.Empty, g.Id);
        Assert.Equal(swimmerId, g.SwimmerId);
        Assert.Equal(relationId, g.RelationId);
        Assert.Equal("Hassan Ali", g.Name); // trimmed
        Assert.Equal("27001010123456", g.NationalId);
        Assert.Equal("+201009876543", g.Phone);
    }

    [Fact]
    public void Update_mutates_name_nationalId_phone()
    {
        var g = new Guardian(Guid.NewGuid(), Guid.NewGuid(), "Old", "27001010123456", "+201000000000");

        g.Update(" Fatima Ibrahim ", "27505050123456", "+201005554444");

        Assert.Equal("Fatima Ibrahim", g.Name);
        Assert.Equal("27505050123456", g.NationalId);
        Assert.Equal("+201005554444", g.Phone);
    }
}
