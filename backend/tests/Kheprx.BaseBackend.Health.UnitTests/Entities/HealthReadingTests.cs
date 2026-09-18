using Kheprx.BaseBackend.Health.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Entities;

public class HealthReadingTests
{
    [Fact]
    public void Ctor_assigns_id_fields_recorder_and_reading_date()
    {
        var swimmerId = Guid.NewGuid();
        var testId = Guid.NewGuid();
        var recordedBy = Guid.NewGuid();

        var r = new HealthReading(swimmerId, testId, 95.5m, recordedBy);

        Assert.NotEqual(Guid.Empty, r.Id);
        Assert.Equal(swimmerId, r.SwimmerId);
        Assert.Equal(testId, r.MedicalTestId);
        Assert.Equal(95.5m, r.Value);
        Assert.Equal(recordedBy, r.RecordedBy);
        Assert.NotEqual(default, r.ReadingDate);
    }
}
