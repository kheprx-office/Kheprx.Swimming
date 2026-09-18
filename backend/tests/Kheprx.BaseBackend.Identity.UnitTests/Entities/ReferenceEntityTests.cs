using Kheprx.BaseBackend.Identity.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class ReferenceEntityTests
{
    [Fact]
    public void Stroke_ctor_trims_and_assigns_id()
    {
        var s = new Stroke("  medley ", " IM ", "  متنوع فردي ");
        Assert.NotEqual(Guid.Empty, s.Id);
        Assert.Equal("medley", s.Code);
        Assert.Equal("IM", s.NameEn);
        Assert.Equal("متنوع فردي", s.NameAr);
    }

    [Fact]
    public void BloodType_blank_name_ar_becomes_null()
    {
        var b = new BloodType("O+", "O+", "   ");
        Assert.Equal("O+", b.Code);
        Assert.Null(b.NameAr);
    }

    [Fact]
    public void Club_ctor_sets_id_created_at_and_has_no_code()
    {
        var c = new Club("Al Ahly", "الأهلي");
        Assert.NotEqual(Guid.Empty, c.Id);
        Assert.Equal("Al Ahly", c.NameEn);
        Assert.Equal("الأهلي", c.NameAr);
        Assert.NotEqual(default, c.CreatedAt);
    }

    [Fact]
    public void ObservationCategory_ctor_trims_and_assigns()
    {
        var c = new ObservationCategory(" allergy ", " Allergy ", " حساسية ");
        Assert.NotEqual(Guid.Empty, c.Id);
        Assert.Equal("allergy", c.Code);
        Assert.Equal("Allergy", c.NameEn);
        Assert.Equal("حساسية", c.NameAr);
    }
}
