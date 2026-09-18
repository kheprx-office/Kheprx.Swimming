using Kheprx.BaseBackend.Health.Application.DTOs;
using Kheprx.BaseBackend.Health.Application.Services;
using Kheprx.BaseBackend.Health.Domain.Entities;
using Kheprx.BaseBackend.Health.Domain.Repositories;
using Moq;
using Xunit;

namespace Kheprx.BaseBackend.Health.UnitTests.Services;

public class HealthReadingServiceTests
{
    private static MedicalTest Glucose() =>
        new("Glucose", "الجلوكوز", "mg/dL", 70m, 110m, Guid.NewGuid());

    private static (HealthReadingService svc, Mock<IHealthReadingRepository> readings) Build(MedicalTest? test)
    {
        var tests = new Mock<IMedicalTestRepository>();
        tests.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(test);
        var readings = new Mock<IHealthReadingRepository>();
        return (new HealthReadingService(readings.Object, tests.Object), readings);
    }

    [Fact]
    public async Task Create_persists_reading_stamps_recorder_and_returns_dto()
    {
        var test = Glucose();
        var (svc, readings) = Build(test);
        HealthReading? added = null;
        readings.Setup(r => r.AddAsync(It.IsAny<HealthReading>(), It.IsAny<CancellationToken>()))
            .Callback<HealthReading, CancellationToken>((r, _) => added = r)
            .Returns(Task.CompletedTask);
        var recordedBy = Guid.NewGuid();
        var req = new CreateHealthReadingRequest(Guid.NewGuid(), test.Id, 95m);

        var dto = await svc.CreateAsync(req, recordedBy);

        Assert.NotNull(dto);
        Assert.NotNull(added);
        Assert.Equal(recordedBy, added!.RecordedBy);
        Assert.Equal(95m, dto!.Value);
        Assert.NotEqual(default, dto.ReadingDate);
        readings.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_derives_normal_status_inside_bounds_and_out_outside()
    {
        var test = Glucose(); // 70..110
        var (svc, _) = Build(test);

        var inside = await svc.CreateAsync(new CreateHealthReadingRequest(Guid.NewGuid(), test.Id, 90m), Guid.NewGuid());
        var low = await svc.CreateAsync(new CreateHealthReadingRequest(Guid.NewGuid(), test.Id, 40m), Guid.NewGuid());
        var boundary = await svc.CreateAsync(new CreateHealthReadingRequest(Guid.NewGuid(), test.Id, 110m), Guid.NewGuid());

        Assert.Equal("normal", inside!.Status);
        Assert.Equal("out", low!.Status);
        Assert.Equal("normal", boundary!.Status); // inclusive upper bound
    }

    [Fact]
    public async Task Create_returns_null_when_test_missing()
    {
        var (svc, readings) = Build(test: null);

        var dto = await svc.CreateAsync(new CreateHealthReadingRequest(Guid.NewGuid(), Guid.NewGuid(), 95m), Guid.NewGuid());

        Assert.Null(dto);
        readings.Verify(r => r.AddAsync(It.IsAny<HealthReading>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
