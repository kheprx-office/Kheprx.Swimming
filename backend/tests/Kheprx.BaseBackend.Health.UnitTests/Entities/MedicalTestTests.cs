using Kheprx.BaseBackend.Health.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Entities;

public class MedicalTestTests
{
    [Fact]
    public void Ctor_assigns_id_fields_creator_and_timestamp()
    {
        var createdBy = Guid.NewGuid();

        var t = new MedicalTest(" Hemoglobin ", " الهيموغلوبين ", " g/dL ", 11m, 17.5m, createdBy);

        Assert.NotEqual(Guid.Empty, t.Id);
        Assert.Equal("Hemoglobin", t.NameEn);
        Assert.Equal("الهيموغلوبين", t.NameAr);
        Assert.Equal("g/dL", t.Unit);
        Assert.Equal(11m, t.LowerBound);
        Assert.Equal(17.5m, t.UpperBound);
        Assert.Equal(createdBy, t.CreatedBy);
        Assert.NotEqual(default, t.CreatedAt);
    }
}
