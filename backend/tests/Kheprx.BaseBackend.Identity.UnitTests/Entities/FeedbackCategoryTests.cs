using Kheprx.BaseBackend.Identity.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class FeedbackCategoryTests
{
    [Fact]
    public void Ctor_trims_fields_and_assigns_id()
    {
        var c = new FeedbackCategory(" technique ", " Technique ", " تكنيك ");
        Assert.NotEqual(Guid.Empty, c.Id);
        Assert.Equal("technique", c.Code);
        Assert.Equal("Technique", c.NameEn);
        Assert.Equal("تكنيك", c.NameAr);
    }

    [Fact]
    public void Ctor_normalizes_blank_arabic_to_null()
    {
        var c = new FeedbackCategory("other", "Other", "   ");
        Assert.Null(c.NameAr);
    }
}
