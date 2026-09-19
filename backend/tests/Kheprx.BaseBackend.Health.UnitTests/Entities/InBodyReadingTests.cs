using Kheprx.BaseBackend.Health.Domain.Entities;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Entities;

public class InBodyReadingTests
{
    [Fact]
    public void Ctor_assigns_id_fields_recorder_and_created_at()
    {
        var swimmerId = Guid.NewGuid();
        var recordedBy = Guid.NewGuid();
        var date = new DateOnly(2024, 10, 4);

        var r = new InBodyReading(swimmerId, date, 180m, 74m, 12.8m, 42.1m, 1.35m, 1.07m, recordedBy);

        Assert.NotEqual(Guid.Empty, r.Id);
        Assert.Equal(swimmerId, r.SwimmerId);
        Assert.Equal(date, r.ReadingDate);
        Assert.Equal(180m, r.HeightCm);
        Assert.Equal(74m, r.WeightKg);
        Assert.Equal(12.8m, r.FatPct);
        Assert.Equal(42.1m, r.MusclePct);
        Assert.Equal(1.35m, r.BoneDensity);
        Assert.Equal(1.07m, r.BodyDensity);
        Assert.Equal(recordedBy, r.RecordedBy);
        Assert.NotEqual(default, r.CreatedAt);
    }

    [Fact]
    public void Update_mutates_reading_date_and_metrics_preserves_recorder_and_created_at()
    {
        var recordedBy = Guid.NewGuid();
        var r = new InBodyReading(Guid.NewGuid(), new DateOnly(2024, 1, 1), 1m, 1m, 1m, 1m, 1m, 1m, recordedBy);
        var createdAt = r.CreatedAt;

        r.Update(new DateOnly(2024, 8, 12), 182m, 75.5m, 14.1m, 41.2m, 1.33m, 1.06m);

        Assert.Equal(new DateOnly(2024, 8, 12), r.ReadingDate);
        Assert.Equal(182m, r.HeightCm);
        Assert.Equal(1.06m, r.BodyDensity);
        Assert.Equal(recordedBy, r.RecordedBy);   // preserved
        Assert.Equal(createdAt, r.CreatedAt);      // preserved
    }
}
