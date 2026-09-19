using Kheprx.BaseBackend.Identity.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class FitnessAssessmentTests
{
    [Fact]
    public void Ctor_trims_and_generates_id()
    {
        var fa = new FitnessAssessment("  fit ", " Fit ", " لائق ");
        Assert.NotEqual(Guid.Empty, fa.Id);
        Assert.Equal("fit", fa.Code);
        Assert.Equal("Fit", fa.NameEn);
        Assert.Equal("لائق", fa.NameAr);
    }

    [Fact]
    public void Ctor_nulls_blank_arabic_name()
    {
        var fa = new FitnessAssessment("unfit", "Unfit", "   ");
        Assert.Null(fa.NameAr);
    }
}
