using Kheprx.BaseBackend.Identity.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class AttendanceStatusTests
{
    [Fact]
    public void Constructor_trims_and_assigns_fields()
    {
        var s = new AttendanceStatus(" present ", " Present ", " حاضر ");
        Assert.NotEqual(Guid.Empty, s.Id);
        Assert.Equal("present", s.Code);
        Assert.Equal("Present", s.NameEn);
        Assert.Equal("حاضر", s.NameAr);
    }

    [Fact]
    public void Constructor_nulls_blank_arabic()
    {
        var s = new AttendanceStatus("absent", "Absent", "   ");
        Assert.Null(s.NameAr);
    }
}
