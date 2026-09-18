using Kheprx.BaseBackend.Health.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Entities;

public class ObservationTests
{
    [Fact]
    public void Ctor_assigns_id_trims_strings_stamps_observed_date_and_recorder()
    {
        var swimmerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var recordedBy = Guid.NewGuid();

        var o = new Observation(swimmerId, categoryId, " Penicillin ", " Severe ", recordedBy);

        Assert.NotEqual(Guid.Empty, o.Id);
        Assert.Equal(swimmerId, o.SwimmerId);
        Assert.Equal(categoryId, o.CategoryId);
        Assert.Equal("Penicillin", o.FieldLabel);
        Assert.Equal("Severe", o.Value);
        Assert.Equal(recordedBy, o.RecordedBy);
        Assert.NotEqual(default, o.ObservedDate);
    }
}
