using Kheprx.BaseBackend.Championships.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Championships.UnitTests.Entities;

public class CompetitionEventTests
{
    [Fact]
    public void Ctor_assigns_id_trims_names_and_collapses_blank_arabic()
    {
        var statusId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var ev = new CompetitionEvent("  Nats  ", "   ", new DateOnly(2023, 11, 15),
            new DateOnly(2023, 11, 16), "  Cairo  ", null, statusId, createdBy);

        Assert.NotEqual(Guid.Empty, ev.Id);
        Assert.Equal("Nats", ev.NameEn);
        Assert.Null(ev.NameAr);              // whitespace-only → null
        Assert.Equal("Cairo", ev.LocationEn);
        Assert.Equal(statusId, ev.StatusId);
        Assert.Equal(createdBy, ev.CreatedBy);
        Assert.Equal(new DateOnly(2023, 11, 16), ev.EndDate);
    }
}
