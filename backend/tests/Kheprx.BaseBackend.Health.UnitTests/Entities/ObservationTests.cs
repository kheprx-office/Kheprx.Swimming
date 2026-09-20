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

    [Fact]
    public void Update_changes_category_label_value_trims_and_preserves_swimmer_observed_date_and_recorder()
    {
        var swimmerId = Guid.NewGuid();
        var recordedBy = Guid.NewGuid();
        var o = new Observation(swimmerId, Guid.NewGuid(), "Penicillin", "Severe", recordedBy);
        var observed = o.ObservedDate;
        var newCat = Guid.NewGuid();

        o.Update(newCat, " Pollen ", " Mild ");

        Assert.Equal(newCat, o.CategoryId);
        Assert.Equal("Pollen", o.FieldLabel);
        Assert.Equal("Mild", o.Value);
        Assert.Equal(swimmerId, o.SwimmerId);       // preserved
        Assert.Equal(recordedBy, o.RecordedBy);     // preserved
        Assert.Equal(observed, o.ObservedDate);     // preserved
    }
}
