using Kheprx.BaseBackend.Identity.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Identity.UnitTests.Entities;

public class BodyMeasurementTests
{
    [Fact]
    public void Ctor_sets_values_new_id_and_today()
    {
        var swimmerId = Guid.NewGuid();

        var m = new BodyMeasurement(swimmerId, 78.5m, 78.2m, 96.2m, 96.0m, 52.8m, 94.0m, 76.5m);

        Assert.NotEqual(Guid.Empty, m.Id);
        Assert.Equal(swimmerId, m.SwimmerId);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), m.MeasuredAt);
        Assert.Equal(78.5m, m.RightArmCm);
        Assert.Equal(78.2m, m.LeftArmCm);
        Assert.Equal(96.2m, m.RightLegCm);
        Assert.Equal(96.0m, m.LeftLegCm);
        Assert.Equal(52.8m, m.TorsoCm);
        Assert.Equal(94.0m, m.BustDiameterCm);
        Assert.Equal(76.5m, m.WaistDiameterCm);
    }
}
